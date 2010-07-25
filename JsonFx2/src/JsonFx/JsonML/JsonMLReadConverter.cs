#region License
/*---------------------------------------------------------------------------------*\

	Distributed under the terms of an MIT-style license:

	The MIT License

	Copyright (c) 2006-2010 Stephen M. McKamey

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in
	all copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
	THE SOFTWARE.

\*---------------------------------------------------------------------------------*/
#endregion License

using System;
using System.Collections.Generic;

using JsonFx.Common;
using JsonFx.IO;
using JsonFx.Markup;
using JsonFx.Serialization;

namespace JsonFx.JsonML
{
	public partial class JsonMLReader
	{
		/// <summary>
		/// Transforms markup tokens into common data tokens using a JsonML model
		/// </summary>
		public class JsonMLReadConverter : IDataTransformer<MarkupTokenType, CommonTokenType>
		{
			#region Constants

			private const string ErrorUnexpectedToken = "Unexpected token ({0})";

			#endregion Constants

			#region Fields

			private ITextStream Scanner = TextReaderStream.Null;

			#endregion Fields

			#region Properties

			/// <summary>
			/// Gets the total number of characters read from the input
			/// </summary>
			public int Column
			{
				get { return this.Scanner.Column; }
			}

			/// <summary>
			/// Gets the total number of lines read from the input
			/// </summary>
			public int Line
			{
				get { return this.Scanner.Line; }
			}

			/// <summary>
			/// Gets the current position within the input
			/// </summary>
			public long Index
			{
				get { return this.Scanner.Index; }
			}

			#endregion Properties

			#region IDataTransformer<CommonTokenType, MarkupTokenType> Members

			/// <summary>
			/// Consumes a sequence of tokens and produces a token sequence of a different type
			/// </summary>
			public IEnumerable<Token<CommonTokenType>> Transform(IEnumerable<Token<MarkupTokenType>> input)
			{
				if (input == null)
				{
					throw new ArgumentNullException("input");
				}

				IStream<Token<MarkupTokenType>> stream = new Stream<Token<MarkupTokenType>>(input);

				bool needsValueDelim = false;
				int depth = 0;

				Token<MarkupTokenType> token = stream.Peek();
				while (!stream.IsCompleted)
				{
					switch (token.TokenType)
					{
						case MarkupTokenType.ElementBegin:
						case MarkupTokenType.ElementVoid:
						{
							if (needsValueDelim)
							{
								yield return CommonGrammar.TokenValueDelim;
								needsValueDelim = false;
							}

							bool isVoid = (token.TokenType == MarkupTokenType.ElementVoid);
							yield return CommonGrammar.TokenArrayBeginNoName;
							yield return CommonGrammar.TokenValue(token.Name);

							stream.Pop();
							token = stream.Peek();
							while (!stream.IsCompleted &&
								token.TokenType == MarkupTokenType.Attribute)
							{
								if (!needsValueDelim)
								{
									needsValueDelim = true;
									yield return CommonGrammar.TokenValueDelim;
									yield return CommonGrammar.TokenObjectBeginNoName;
								}
								else
								{
									yield return CommonGrammar.TokenValueDelim;
								}

								yield return CommonGrammar.TokenProperty(token.Name);

								stream.Pop();
								token = stream.Peek();

								switch (token.TokenType)
								{
									case MarkupTokenType.TextValue:
									case MarkupTokenType.Whitespace:
									{
										yield return CommonGrammar.TokenValue(token.Value);
										break;
									}
									case MarkupTokenType.UnparsedBlock:
									{
										yield return new Token<CommonTokenType>(CommonTokenType.Primitive, token.Name, token.Value);
										break;
									}
									default:
									{
										throw new TokenException<MarkupTokenType>(
											token,
											String.Format(ErrorUnexpectedToken, token.TokenType));
									}
								}

								stream.Pop();
								token = stream.Peek();
							}

							if (needsValueDelim)
							{
								yield return CommonGrammar.TokenObjectEnd;
							}
							else
							{
								needsValueDelim = true;
							}

							if (isVoid)
							{
								yield return CommonGrammar.TokenArrayEnd;
								needsValueDelim = true;
							}
							else
							{
								depth++;
							}
							break;
						}
						case MarkupTokenType.ElementEnd:
						{
							if (depth > 0)
							{
								yield return CommonGrammar.TokenArrayEnd;
								needsValueDelim = true;
							}
							depth--;

							stream.Pop();
							token = stream.Peek();
							break;
						}
						case MarkupTokenType.TextValue:
						case MarkupTokenType.Whitespace:
						{
							if (needsValueDelim)
							{
								yield return CommonGrammar.TokenValueDelim;
							}

							string value = token.ValueAsString();

							stream.Pop();
							token = stream.Peek();
							while (!stream.IsCompleted &&
								(token.TokenType == MarkupTokenType.TextValue ||
								token.TokenType == MarkupTokenType.Whitespace))
							{
								// concatenate adjacent value nodes
								value = String.Concat(value, token.ValueAsString());

								stream.Pop();
								token = stream.Peek();
							}

							yield return CommonGrammar.TokenValue(value);
							needsValueDelim = true;
							break;
						}
						case MarkupTokenType.UnparsedBlock:
						{
							if (needsValueDelim)
							{
								yield return CommonGrammar.TokenValueDelim;
							}
							yield return new Token<CommonTokenType>(CommonTokenType.Primitive, token.Name, token.Value);
							needsValueDelim = true;

							stream.Pop();
							token = stream.Peek();
							break;
						}
						case MarkupTokenType.Attribute:
						default:
						{
							throw new TokenException<MarkupTokenType>(
								token,
								String.Format(ErrorUnexpectedToken, token.TokenType));
						}
					}
				}

				while (depth > 0)
				{
					depth--;
					yield return CommonGrammar.TokenArrayEnd;
				}
			}

			#endregion IDataTransformer<CommonTokenType, MarkupTokenType> Members
		}
	}
}
