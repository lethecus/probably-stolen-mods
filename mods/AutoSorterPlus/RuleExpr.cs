using System;
using System.Collections.Generic;
using System.Text;

namespace AutoSorterPlus
{
	internal sealed class RuleParseException : Exception
	{
		internal readonly int Position;

		internal RuleParseException(string message, int position) : base(message)
		{
			Position = position;
		}
	}

	internal abstract class RuleNode
	{
		internal abstract bool Eval(ItemFacts facts);
	}

	internal sealed class AndNode : RuleNode
	{
		private readonly RuleNode _left;
		private readonly RuleNode _right;

		internal AndNode(RuleNode left, RuleNode right)
		{
			_left = left;
			_right = right;
		}

		internal override bool Eval(ItemFacts facts)
		{
			return _left.Eval(facts) && _right.Eval(facts);
		}
	}

	internal sealed class OrNode : RuleNode
	{
		private readonly RuleNode _left;
		private readonly RuleNode _right;

		internal OrNode(RuleNode left, RuleNode right)
		{
			_left = left;
			_right = right;
		}

		internal override bool Eval(ItemFacts facts)
		{
			return _left.Eval(facts) || _right.Eval(facts);
		}
	}

	internal sealed class NotNode : RuleNode
	{
		private readonly RuleNode _inner;

		internal NotNode(RuleNode inner)
		{
			_inner = inner;
		}

		internal override bool Eval(ItemFacts facts)
		{
			return !_inner.Eval(facts);
		}
	}

	internal enum TermKind
	{
		Tag,
		Id,
		Name,
		Mod,
		Label,
		Stolen,
		Contraband,
		Owned
	}

	internal sealed class TermNode : RuleNode
	{
		private readonly TermKind _kind;
		private readonly string _pattern;
		private readonly bool _wildcard;

		internal TermNode(TermKind kind, string pattern)
		{
			_kind = kind;
			_pattern = pattern ?? "";
			_wildcard = _pattern.IndexOf('*') >= 0 || _pattern.IndexOf('?') >= 0;
		}

		internal override bool Eval(ItemFacts facts)
		{
			switch (_kind)
			{
			case TermKind.Stolen:
				return facts.Stolen;
			case TermKind.Contraband:
				return facts.Contraband;
			case TermKind.Owned:
				return facts.Owned;
			case TermKind.Id:
				return Glob.IsMatch(_pattern, facts.Id);
			case TermKind.Name:
				return Glob.IsMatch(_pattern, facts.Name) || Glob.IsMatch(_pattern, facts.Id);
			case TermKind.Mod:
				if (IsBaseKeyword(_pattern))
				{
					return string.IsNullOrEmpty(facts.ModId);
				}
				return Glob.IsMatch(_pattern, facts.ModId) || Glob.IsMatch(_pattern, facts.ModName);
			case TermKind.Label:
				if (facts.Labels == null)
				{
					return false;
				}
				foreach (string label in facts.Labels)
				{
					if (Glob.IsMatch(_pattern, label))
					{
						return true;
					}
				}
				return false;
			default:
				if (!_wildcard)
				{
					return facts.Types.Contains(_pattern);
				}
				foreach (string type in facts.Types)
				{
					if (Glob.IsMatch(_pattern, type))
					{
						return true;
					}
				}
				return false;
			}
		}

		private static bool IsBaseKeyword(string pattern)
		{
			return string.Equals(pattern, "base", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(pattern, "vanilla", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(pattern, "none", StringComparison.OrdinalIgnoreCase);
		}
	}

	// Grammar (case-insensitive keywords, implicit AND between terms):
	//   or   := and (OR and)*
	//   and  := unary ((AND)? unary)*
	//   unary:= NOT unary | "(" or ")" | term
	//   term := tag:|type:|id:|name:|mod:|modid:|modname: <glob> | stolen | contraband | owned | <glob>
	internal static class RuleExpr
	{
		private enum TokenKind
		{
			Word,
			Open,
			Close
		}

		private struct Token
		{
			internal TokenKind Kind;
			internal string Text;
			internal int Position;
		}

		// Returns null for an empty expression (meaning: no filter).
		internal static RuleNode Parse(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			List<Token> tokens = Tokenize(text);
			int index = 0;
			RuleNode node = ParseOr(tokens, ref index);
			if (index < tokens.Count)
			{
				throw new RuleParseException(tokens[index].Text, tokens[index].Position);
			}
			return node;
		}

		internal static bool TryParse(string text, out RuleNode node, out string error)
		{
			node = null;
			error = null;
			try
			{
				node = Parse(text);
				return true;
			}
			catch (RuleParseException exception)
			{
				error = exception.Message;
				return false;
			}
			catch (Exception exception)
			{
				error = exception.Message;
				return false;
			}
		}

		private static List<Token> Tokenize(string text)
		{
			List<Token> tokens = new List<Token>();
			int i = 0;
			while (i < text.Length)
			{
				char c = text[i];
				if (char.IsWhiteSpace(c))
				{
					i++;
					continue;
				}
				if (c == '(')
				{
					tokens.Add(new Token { Kind = TokenKind.Open, Text = "(", Position = i });
					i++;
					continue;
				}
				if (c == ')')
				{
					tokens.Add(new Token { Kind = TokenKind.Close, Text = ")", Position = i });
					i++;
					continue;
				}
				if (c == ',')
				{
					// Legacy list style: commas separate alternatives (OR).
					tokens.Add(new Token { Kind = TokenKind.Word, Text = ",", Position = i });
					i++;
					continue;
				}
				int start = i;
				StringBuilder builder = new StringBuilder();
				bool quoted = false;
				// Values for these prefixes may contain spaces (display names), so read
				// the whole phrase up to the next operator instead of splitting it.
				string[] phrasePrefixes = { "modname:", "name:", "mod:" };
				string phrasePrefix = null;
				foreach (string prefix in phrasePrefixes)
				{
					if (i + prefix.Length <= text.Length && string.Compare(text, i, prefix, 0, prefix.Length, true) == 0)
					{
						phrasePrefix = prefix;
						break;
					}
				}
				if (phrasePrefix != null)
				{
					builder.Append(text, i, phrasePrefix.Length);
					i += phrasePrefix.Length;
					if (i < text.Length && text[i] == '"')
					{
						quoted = true;
						i++;
						while (i < text.Length && text[i] != '"')
						{
							builder.Append(text[i++]);
						}
						if (i < text.Length && text[i] == '"')
						{
							i++;
						}
					}
					else
					{
						while (i < text.Length)
						{
							char ch = text[i];
							if (ch == ')' || ch == ',' || ch == '(')
							{
								break;
							}
							if (char.IsWhiteSpace(ch))
							{
								int look = i;
								while (look < text.Length && char.IsWhiteSpace(text[look]))
								{
									look++;
								}
								if (look >= text.Length)
								{
									break;
								}
								int end = look;
								while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != ')' && text[end] != '(' && text[end] != ',')
								{
									end++;
								}
								string next = text.Substring(look, end - look);
								if (IsOperator(next))
								{
									break;
								}
								builder.Append(' ');
								i = look;
								continue;
							}
							builder.Append(ch);
							i++;
						}
					}
				}
				else
				{
					while (i < text.Length)
					{
						char ch = text[i];
						if (ch == '"')
						{
							quoted = !quoted;
							i++;
							continue;
						}
						if (!quoted && (char.IsWhiteSpace(ch) || ch == '(' || ch == ')'))
						{
							break;
						}
						builder.Append(ch);
						i++;
					}
				}
				if (quoted)
				{
					throw new RuleParseException("Unterminated quote", start);
				}
				tokens.Add(new Token { Kind = TokenKind.Word, Text = builder.ToString().Trim(), Position = start });
			}
			return tokens;
		}

		private static RuleNode ParseOr(List<Token> tokens, ref int index)
		{
			RuleNode left = ParseAnd(tokens, ref index);
			while (index < tokens.Count && IsWord(tokens[index], "OR", "||", ","))
			{
				index++;
				RuleNode right = ParseAnd(tokens, ref index);
				left = new OrNode(left, right);
			}
			return left;
		}

		private static RuleNode ParseAnd(List<Token> tokens, ref int index)
		{
			RuleNode left = ParseUnary(tokens, ref index);
			while (index < tokens.Count)
			{
				if (IsWord(tokens[index], "AND", "&&"))
				{
					index++;
				}
				else if (IsWord(tokens[index], "OR", "||", ",") || tokens[index].Kind == TokenKind.Close)
				{
					break;
				}
				RuleNode right = ParseUnary(tokens, ref index);
				left = new AndNode(left, right);
			}
			return left;
		}

		private static RuleNode ParseUnary(List<Token> tokens, ref int index)
		{
			if (index >= tokens.Count)
			{
				throw new RuleParseException("Expression ends unexpectedly", -1);
			}
			Token token = tokens[index];
			if (token.Kind == TokenKind.Word && IsWord(token, "NOT", "!"))
			{
				index++;
				return new NotNode(ParseUnary(tokens, ref index));
			}
			if (token.Kind == TokenKind.Open)
			{
				index++;
				RuleNode inner = ParseOr(tokens, ref index);
				if (index >= tokens.Count || tokens[index].Kind != TokenKind.Close)
				{
					throw new RuleParseException("Missing ')'", token.Position);
				}
				index++;
				return inner;
			}
			if (token.Kind == TokenKind.Close)
			{
				throw new RuleParseException("Unexpected ')'", token.Position);
			}
			index++;
			return ParseTerm(token);
		}

		private static RuleNode ParseTerm(Token token)
		{
			string word = token.Text;
			if (word.Length == 0)
			{
				throw new RuleParseException("Empty term", token.Position);
			}
			int colon = word.IndexOf(':');
			if (colon <= 0)
			{
				if (string.Equals(word, "stolen", StringComparison.OrdinalIgnoreCase))
				{
					return new TermNode(TermKind.Stolen, "");
				}
				if (string.Equals(word, "contraband", StringComparison.OrdinalIgnoreCase))
				{
					return new TermNode(TermKind.Contraband, "");
				}
				if (string.Equals(word, "owned", StringComparison.OrdinalIgnoreCase))
				{
					return new TermNode(TermKind.Owned, "");
				}
				if (!string.IsNullOrEmpty(word) && (word[0] == '!' || word[0] == '-'))
				{
					return new NotNode(new TermNode(TermKind.Tag, word.Substring(1)));
				}
				return new TermNode(TermKind.Tag, word);
			}
			string prefix = word.Substring(0, colon);
			string value = word.Substring(colon + 1);
			if (value.Length == 0)
			{
				throw new RuleParseException("Missing value after '" + prefix + ":'", token.Position);
			}
			switch (prefix.ToLowerInvariant())
			{
			case "tag":
			case "type":
				return new TermNode(TermKind.Tag, value);
			case "id":
				return new TermNode(TermKind.Id, value);
			case "name":
				return new TermNode(TermKind.Name, value);
			case "mod":
			case "modid":
			case "modname":
				return new TermNode(TermKind.Mod, value);
			case "label":
			case "condition":
			case "feature":
				return new TermNode(TermKind.Label, value);
			default:
				return new TermNode(TermKind.Tag, word);
			}
		}

		private static bool IsOperator(string word)
		{
			return string.Equals(word, "AND", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(word, "OR", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(word, "NOT", StringComparison.OrdinalIgnoreCase)
				|| word == "&&"
				|| word == "||"
				|| word == "!";
		}

		private static bool IsWord(Token token, params string[] words)
		{
			if (token.Kind != TokenKind.Word)
			{
				return false;
			}
			foreach (string word in words)
			{
				if (string.Equals(token.Text, word, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}
	}
}
