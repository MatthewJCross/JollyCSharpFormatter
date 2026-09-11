using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace JollyCSharpFormatter.Formatting
{
	public static class CSharpFormatter
	{
        public static string Format(string source, FormatterOptions? options = null)
        {
            options ??= new FormatterOptions();

            var tree = CSharpSyntaxTree.ParseText(source);
            var diagnostics = tree.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error).ToList();
            if (diagnostics.Count > 0)
                throw new InvalidOperationException("The source contains C# syntax errors and could not be formatted safely.");

            var root = tree.GetRoot();
            root = new SingleLineRewriter(options).Visit(root)!;

            using var workspace = new AdhocWorkspace();
            var formatted = Formatter.Format(root, workspace);
            var result = formatted.ToFullString();

            if (options.SingleLineConditions)
                result = FormatConditions(result);

            result = FormatControlStatements(result, options);
            result = FormatStatements(result, options);
            result = FormatMembers(result, options);
            result = ExpandSingleLineBlocks(result, options);
            result = FormatSimpleStatements(result);

            if (options.UseTabs)
                result = ConvertLeadingSpacesToTabs(result);

            result = NormalizeBlankLines(result);
            return result;
        }

        private static string FormatSimpleStatements(string source)
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();
            var edits = new List<TextEdit>();

            foreach (var statement in root.DescendantNodes().OfType<StatementSyntax>())
            {
                if (!IsSimpleStatement(statement))
                    continue;

                FormatSimpleStatement(statement, source, edits);
            }

            ApplyEdits(ref source, edits);

            return MergeStringConcatenations(source);
        }

        private static string MergeStringConcatenations(string source)
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();
            var edits = new List<TextEdit>();

            foreach (var statement in root.DescendantNodes().OfType<StatementSyntax>())
            {
                if (!IsSimpleStatement(statement))
                    continue;

                foreach (var binary in statement.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>())
                {
                    if (!binary.IsKind(SyntaxKind.AddExpression))
                        continue;

                    if (binary.Left is not LiteralExpressionSyntax &&
                        binary.Left is not InterpolatedStringExpressionSyntax)
                    {
                        continue;
                    }

                    if (binary.Right is not LiteralExpressionSyntax &&
                        binary.Right is not InterpolatedStringExpressionSyntax)
                    {
                        continue;
                    }

                    if (!TryMergeStringExpressions(binary.Left, binary.Right, out var replacement))
                        continue;

                    edits.Add(new TextEdit(binary.SpanStart, binary.Span.Length, replacement));
                }
            }

            ApplyEdits(ref source, edits);
            return source;
        }

        private static bool TryMergeStringExpressions(ExpressionSyntax left, ExpressionSyntax right, out string replacement)
        {
            replacement = string.Empty;

            if (!TryGetStringExpressionParts(left, out var leftText, out var leftInterpolated))
                return false;

            if (!TryGetStringExpressionParts(right, out var rightText, out var rightInterpolated))
                return false;

            if (leftInterpolated || rightInterpolated)
            {
                var combined = leftText + rightText;
                replacement = "$\"" + combined + "\"";
                return true;
            }

            replacement = "\"" + leftText + rightText + "\"";
            return true;
        }

        private static bool TryGetStringExpressionParts(ExpressionSyntax expression, out string text, out bool interpolated)
        {
            text = string.Empty;
            interpolated = false;

            if (expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                var value = literal.Token.ValueText;
                text = EscapeStringContent(value);
                return true;
            }

            if (expression is InterpolatedStringExpressionSyntax interpolatedString)
            {
                var fullText = interpolatedString.ToFullString().Trim();

                if (!fullText.StartsWith("$\"", StringComparison.Ordinal) ||
                    !fullText.EndsWith("\"", StringComparison.Ordinal))
                {
                    return false;
                }

                text = fullText[2..^1];
                interpolated = true;
                return true;
            }

            return false;
        }

        private static string EscapeStringContent(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static void FormatSimpleStatement(StatementSyntax statement, string source, List<TextEdit> edits)
        {
            var tokens = statement.DescendantTokens().ToList();

            for (var i = 0; i < tokens.Count - 1; i++)
            {
                var current = tokens[i];
                var next = tokens[i + 1];

                var start = current.Span.End;
                var end = next.SpanStart;

                if (end <= start)
                    continue;

                var whitespace = source[start..end];

                if (!whitespace.Contains('\n') && !whitespace.Contains('\r'))
                    continue;

                if (whitespace.Contains("//") || whitespace.Contains("/*") || whitespace.Contains("*/"))
                    continue;

                var replacement = whitespace
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n');

                if (replacement.All(char.IsWhiteSpace))
                    edits.Add(new TextEdit(start, end - start, " "));
            }
        }

        private static bool IsSimpleStatement(StatementSyntax statement)
        {
            if (statement is BlockSyntax)
                return false;

            if (statement is IfStatementSyntax)
                return false;

            if (statement is WhileStatementSyntax)
                return false;

            if (statement is ForStatementSyntax)
                return false;

            if (statement is ForEachStatementSyntax)
                return false;

            if (statement is DoStatementSyntax)
                return false;

            if (statement is SwitchStatementSyntax)
                return false;

            if (statement is TryStatementSyntax)
                return false;

            if (statement is UsingStatementSyntax usingStatement &&
                usingStatement.Statement is BlockSyntax)
                return false;

            if (statement.DescendantNodes().OfType<InitializerExpressionSyntax>().Any())
                return false;

            return true;
        }

        private static string FormatConditions(string source)
		{
			var tree = CSharpSyntaxTree.ParseText(source);
			var root = tree.GetRoot();
			var edits = new List<TextEdit>();
			foreach (var ifStatement in root.DescendantNodes().OfType<IfStatementSyntax>())
				FormatCondition(ifStatement.Condition, source, edits);

			foreach (var whileStatement in root.DescendantNodes().OfType<WhileStatementSyntax>())
				FormatCondition(whileStatement.Condition, source, edits);

			foreach (var forStatement in root.DescendantNodes().OfType<ForStatementSyntax>())
			{
				if (forStatement.Condition is not null)
					FormatCondition(forStatement.Condition, source, edits);
			}

			foreach (var forEachStatement in root.DescendantNodes().OfType<ForEachStatementSyntax>())
				FormatCondition(forEachStatement.Expression, source, edits);

			ApplyEdits(ref source, edits);
			return source;
		}

		private static void FormatCondition(ExpressionSyntax expression, string source, List<TextEdit> edits)
		{
			var start = expression.SpanStart;
			var end = expression.Span.End;
			if (end <= start)
				return;

			var text = source[start..end];
			if (!text.Contains('\n') && !text.Contains('\r'))
				return;

			var singleLine = string.Join(" ", text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
			edits.Add(new TextEdit(start, end - start, singleLine));
		}

        private static string FormatControlStatements(string source, FormatterOptions options)
        {
			var tree = CSharpSyntaxTree.ParseText(source);
			var root = tree.GetRoot();
			var edits = new List<TextEdit>();
			foreach (var node in root.DescendantNodes())
			{
				switch (node)
				{
					case IfStatementSyntax ifStatement:
						ProcessControlStatement(ifStatement.CloseParenToken, ifStatement.Statement, source, edits, options);
                        ProcessElseClause(ifStatement.Else, source, edits, options); 
						break;

					case WhileStatementSyntax whileStatement:
						ProcessControlStatement(whileStatement.CloseParenToken, whileStatement.Statement, source, edits, options);
						break;

					case ForStatementSyntax forStatement:
						ProcessControlStatement(forStatement.CloseParenToken, forStatement.Statement, source, edits, options);
						break;

					case ForEachStatementSyntax forEachStatement:
						ProcessControlStatement(forEachStatement.CloseParenToken, forEachStatement.Statement, source, edits, options);
						break;
				}
			}

			ApplyEdits(ref source, edits);
			return source;
		}

        private static void ProcessControlStatement(SyntaxToken closeParen, StatementSyntax statement, string source, List<TextEdit> edits, FormatterOptions options)
        {
			if (statement is BlockSyntax)
				return;

			var conditionLine = closeParen.GetLocation().GetLineSpan().StartLinePosition.Line;
			var statementLine = statement.GetLocation().GetLineSpan().StartLinePosition.Line;
			if (conditionLine != statementLine)
				return;

			var start = closeParen.Span.End;
			var end = statement.SpanStart;
			if (end <= start)
				return;

			var indentation = GetIndentationAt(source, closeParen.SpanStart);
            edits.Add(new TextEdit(start, end - start, Environment.NewLine + GetInnerIndentation(indentation, options)));
        }

        private static void ProcessElseClause(ElseClauseSyntax? elseClause, string source, List<TextEdit> edits, FormatterOptions options)
        {
			if (elseClause is null)
				return;

			if (elseClause.Statement is BlockSyntax)
				return;

			if (elseClause.Statement is IfStatementSyntax)
				return;

			var elseLine = elseClause.ElseKeyword.GetLocation().GetLineSpan().StartLinePosition.Line;
			var statementLine = elseClause.Statement.GetLocation().GetLineSpan().StartLinePosition.Line;
			if (elseLine != statementLine)
				return;

			var start = elseClause.ElseKeyword.Span.End;
			var end = elseClause.Statement.SpanStart;
			if (end <= start)
				return;

			var indentation = GetIndentationAt(source, elseClause.ElseKeyword.SpanStart);
            edits.Add(new TextEdit(start, end - start, Environment.NewLine + GetInnerIndentation(indentation, options)));
        }

        private static string FormatStatements(string source, FormatterOptions options)
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();
            var edits = new List<TextEdit>();

            foreach (var block in root.DescendantNodes().OfType<BlockSyntax>())
                ProcessBlockStatements(block, source, edits, options);

            ApplyEdits(ref source, edits);
            return source;
        }

        private static void ProcessBlockStatements(BlockSyntax block, string source, List<TextEdit> edits, FormatterOptions options)
        {
            var statements = block.Statements;
            if (statements.Count == 0)
                return;

            var blockIndentation = GetIndentationAt(source, block.OpenBraceToken.SpanStart);
            var statementIndentation = GetInnerIndentation(blockIndentation, options);

            for (var i = 0; i < statements.Count; i++)
            {
                var statement = statements[i];
                var previousEnd = i == 0 ? block.OpenBraceToken.Span.End : statements[i - 1].Span.End;
                var start = statement.SpanStart;

                if (start <= previousEnd)
                    continue;

                var needsBlankLine = false;

                if (i > 0)
                {
                    var previousStatement = statements[i - 1];

                    if (options.BlankLineAfterBlocks &&
                        previousStatement.GetLastToken().IsKind(SyntaxKind.CloseBraceToken))
                    {
                        needsBlankLine = true;
                    }

                    if (options.BlankLineAfterControlStatements &&
                        IsUnbracedControlStatement(previousStatement))
                    {
                        needsBlankLine = true;
                    }

                    if (options.BlankLineAfterBlocks &&
                        previousStatement is LocalDeclarationStatementSyntax local &&
                        local.Declaration.Variables.Any(variable =>
                            variable.Initializer?.Value is ObjectCreationExpressionSyntax objectCreation &&
                            objectCreation.Initializer is not null))
                    {
                        needsBlankLine = true;
                    }
                }

                var newlineCount = needsBlankLine ? 2 : 1;
                var replacement = string.Concat(Enumerable.Repeat(Environment.NewLine, newlineCount)) + statementIndentation;

                edits.Add(new TextEdit(previousEnd, start - previousEnd, replacement));
            }
        }

        private static bool IsUnbracedControlStatement(StatementSyntax statement)
        {
            if (statement is IfStatementSyntax ifStatement)
                return ifStatement.Statement is not BlockSyntax;

            if (statement is WhileStatementSyntax whileStatement)
                return whileStatement.Statement is not BlockSyntax;

            if (statement is ForStatementSyntax forStatement)
                return forStatement.Statement is not BlockSyntax;

            if (statement is ForEachStatementSyntax forEachStatement)
                return forEachStatement.Statement is not BlockSyntax;

            if (statement is DoStatementSyntax doStatement)
                return doStatement.Statement is not BlockSyntax;

            return false;
        }

		private static string FormatMembers(string source, FormatterOptions options)
		{
            if (!options.BlankLineBetweenMembers)
                return source;
            
			var tree = CSharpSyntaxTree.ParseText(source);
			var root = tree.GetRoot();
			var edits = new List<TextEdit>();
			foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
			{
				ProcessTypeMembers(type, source, edits);
			}

			ApplyEdits(ref source, edits);
			return source;
		}

        private static string ExpandSingleLineBlocks(string source, FormatterOptions options)
        {
            if (!options.ExpandBlocks)
                return source;

            var tree = CSharpSyntaxTree.ParseText(source);
			var root = tree.GetRoot();
			var edits = new List<TextEdit>();
			foreach (var block in root.DescendantNodes().OfType<BlockSyntax>())
                FormatBlockBraces(block, source, edits, options);

            foreach (var initializer in root.DescendantNodes().OfType<InitializerExpressionSyntax>())
                FormatInitializer(initializer, source, edits, options);

            ApplyEdits(ref source, edits);
			return source;
		}

        private static void FormatBlockBraces(BlockSyntax block, string source, List<TextEdit> edits, FormatterOptions options)
        {
			var openBrace = block.OpenBraceToken;
			var closeBrace = block.CloseBraceToken;
			if (openBrace.IsMissing || closeBrace.IsMissing)
				return;

			var indentation = GetIndentationAt(source, openBrace.SpanStart);
			var previousToken = openBrace.GetPreviousToken();
			if (!previousToken.IsKind(SyntaxKind.None))
			{
				var start = previousToken.Span.End;
				var end = openBrace.SpanStart;
				if (end > start)
				{
					var whitespace = source[start..end];
					if (!whitespace.Contains('\n') && !whitespace.Contains('\r'))
						edits.Add(new TextEdit(start, end - start, Environment.NewLine + GetInnerIndentation(indentation, options)));
				}
			}

			if (block.Statements.Count == 0)
				return;

			var firstStatement = block.Statements[0];
			var startExpression = openBrace.Span.End;
			var endExpression = firstStatement.SpanStart;
			if (endExpression > startExpression)
			{
				var whitespace = source[startExpression..endExpression];
				if (!whitespace.Contains('\n') && !whitespace.Contains('\r'))
                    edits.Add(new TextEdit(startExpression, endExpression - startExpression, Environment.NewLine + GetInnerIndentation(indentation, options)));
            }

			var lastStatement = block.Statements[^1];
			var closeStart = closeBrace.SpanStart;
			var lastEnd = lastStatement.Span.End;
			if (closeStart > lastEnd)
			{
				var whitespace = source[lastEnd..closeStart];
				if (!whitespace.Contains('\n') && !whitespace.Contains('\r'))
					edits.Add(new TextEdit(lastEnd, closeStart - lastEnd, Environment.NewLine + indentation));
			}
		}

        private static void FormatInitializer(InitializerExpressionSyntax initializer, string source, List<TextEdit> edits, FormatterOptions options)
        {
			var openBrace = initializer.OpenBraceToken;
			var closeBrace = initializer.CloseBraceToken;
			if (openBrace.IsMissing || closeBrace.IsMissing || initializer.Expressions.Count == 0)
				return;

			var indentation = GetIndentationAt(source, openBrace.SpanStart);
            var innerIndentation = GetInnerIndentation(indentation, options); 
			var previousToken = openBrace.GetPreviousToken();
			if (!previousToken.IsKind(SyntaxKind.None))
			{
				var start = previousToken.Span.End;
				var end = openBrace.SpanStart;
				if (end > start)
				{
					var whitespace = source[start..end];
					if (!whitespace.Contains('\n') && !whitespace.Contains('\r'))
						edits.Add(new TextEdit(start, end - start, Environment.NewLine + indentation));
				}
			}

			var firstExpression = initializer.Expressions[0];
			var startExpression = openBrace.Span.End;
			var endExpression = firstExpression.SpanStart;
			if (endExpression > startExpression)
			{
				var whitespace = source[startExpression..endExpression];
				if (!whitespace.Contains('\n') && !whitespace.Contains('\r'))
					edits.Add(new TextEdit(startExpression, endExpression - startExpression, Environment.NewLine + innerIndentation));
			}

			for (var i = 0; i < initializer.Expressions.Count - 1; i++)
			{
				var expression = initializer.Expressions[i];
				var comma = expression.GetLastToken().GetNextToken();
				if (!comma.IsKind(SyntaxKind.CommaToken))
					continue;

				var nextExpression = initializer.Expressions[i + 1];
				var start = comma.Span.End;
				var end = nextExpression.SpanStart;
				if (end <= start)
					continue;

				var whitespace = source[start..end];
				if (!whitespace.Contains('\n') && !whitespace.Contains('\r'))
					edits.Add(new TextEdit(start, end - start, Environment.NewLine + innerIndentation));
			}

			var lastExpression = initializer.Expressions[^1];
			var lastEnd = lastExpression.Span.End;
			var closeStart = closeBrace.SpanStart;
			if (closeStart > lastEnd)
			{
				var whitespace = source[lastEnd..closeStart];
				if (!whitespace.Contains('\n') && !whitespace.Contains('\r'))
					edits.Add(new TextEdit(lastEnd, closeStart - lastEnd, Environment.NewLine + indentation));
			}
		}

        private static void ProcessTypeMembers(TypeDeclarationSyntax type, string source, List<TextEdit> edits)
        {
            var members = type.Members;

            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                EnsureMemberOpeningBrace(member, source, edits);

                if (i == 0)
                    continue;

                var previousMember = members[i - 1];
                var previousEnd = previousMember.Span.End;
                var currentStart = member.SpanStart;

                if (currentStart <= previousEnd)
                    continue;

                var whitespace = source[previousEnd..currentStart];

                var previousIsFunction = previousMember is MethodDeclarationSyntax ||
                                         previousMember is ConstructorDeclarationSyntax ||
                                         previousMember is DestructorDeclarationSyntax ||
                                         previousMember is OperatorDeclarationSyntax ||
                                         previousMember is ConversionOperatorDeclarationSyntax;

                var currentIsFunction = member is MethodDeclarationSyntax ||
                                        member is ConstructorDeclarationSyntax ||
                                        member is DestructorDeclarationSyntax ||
                                        member is OperatorDeclarationSyntax ||
                                        member is ConversionOperatorDeclarationSyntax;

                if (!currentIsFunction)
                    continue;

                if (CountNewLines(whitespace) >= 2)
                    continue;

                var indentation = GetIndentationAt(source, currentStart);
                edits.Add(new TextEdit(previousEnd, currentStart - previousEnd, Environment.NewLine + Environment.NewLine + indentation));
            }
        }

        private static void EnsureMemberOpeningBrace(MemberDeclarationSyntax member, string source, List<TextEdit> edits)
		{
			if (member is not BaseTypeDeclarationSyntax && member is not MethodDeclarationSyntax && member is not ConstructorDeclarationSyntax && member is not PropertyDeclarationSyntax && member is not IndexerDeclarationSyntax && member is not EventDeclarationSyntax)
			{
				return;
			}

			var openBrace = member.DescendantTokens().FirstOrDefault(x => x.IsKind(SyntaxKind.OpenBraceToken));
			if (openBrace.IsKind(SyntaxKind.None))
				return;

			var previousToken = openBrace.GetPreviousToken();
			if (previousToken.IsKind(SyntaxKind.None))
				return;

			var start = previousToken.Span.End;
			var end = openBrace.SpanStart;
			if (end <= start)
				return;

			var whitespace = source[start..end];
			if (CountNewLines(whitespace) >= 1)
				return;

			var indentation = GetIndentationAt(source, openBrace.SpanStart);
			edits.Add(new TextEdit(start, end - start, Environment.NewLine + indentation));
		}

		private static void ApplyEdits(ref string source, List<TextEdit> edits)
		{
			foreach (var edit in edits.OrderByDescending(x => x.Start).ThenByDescending(x => x.Length))
			{
				if (edit.Start < 0 || edit.Start > source.Length || edit.Start + edit.Length > source.Length)
				{
					continue;
				}

				source = source.Remove(edit.Start, edit.Length);
				source = source.Insert(edit.Start, edit.Replacement);
			}
		}

		private static int CountNewLines(string text)
		{
			return text.Count(c => c == '\n');
		}

        private static string GetInnerIndentation(string indentation, FormatterOptions options)
        {
            return indentation + (options.UseTabs ? "\t" : "    ");
        }
        
		private static string GetIndentationAt(string source, int position)
		{
			if (position <= 0)
				return string.Empty;

			var lineStart = source.LastIndexOf('\n', position - 1);
			if (lineStart < 0)
				lineStart = 0;
			else
				lineStart++;

			var index = lineStart;
			while (index < source.Length && (source[index] == ' ' || source[index] == '\t'))
			{
				index++;
			}

			return source[lineStart..index];
		}

		private static string ConvertLeadingSpacesToTabs(string source)
		{
			var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				var spaces = 0;
				while (spaces < line.Length && line[spaces] == ' ')
				{
					spaces++;
				}

				if (spaces == 0)
					continue;

				var tabs = spaces / 4;
				var remainder = spaces % 4;
				lines[i] = new string('\t', tabs) + new string(' ', remainder) + line[spaces..];
			}

			return string.Join(Environment.NewLine, lines);
		}

		private static string NormalizeBlankLines(string source)
		{
			var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			var result = new List<string>();
			var blankLineCount = 0;
			foreach (var line in lines)
			{
				if (string.IsNullOrWhiteSpace(line))
				{
					blankLineCount++;
					if (blankLineCount > 1)
						continue;

					result.Add(string.Empty);
					continue;
				}

				blankLineCount = 0;
				result.Add(line);
			}

			return string.Join(Environment.NewLine, result);
		}

		private sealed class SingleLineRewriter : CSharpSyntaxRewriter
		{
			private readonly FormatterOptions _options;

			public SingleLineRewriter(FormatterOptions options)
			{
				_options = options;
			}

			public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
			{
				node = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
				if (_options.SingleLineMethodDeclarations)
				{
					node = node.WithParameterList(OneLine(node.ParameterList));
				}

				return node;
			}

			public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
			{
				node = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
				if (_options.SingleLineConstructorDeclarations)
				{
					node = node.WithParameterList(OneLine(node.ParameterList));
				}

				return node;
			}

            public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
            {
                node = (RecordDeclarationSyntax)base.VisitRecordDeclaration(node)!;

                if (_options.SingleLineRecordDeclarations && node.ParameterList is not null)
                    node = node.WithParameterList(OneLine(node.ParameterList));

                return node;
            }

            public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
			{
				node = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
				if (_options.SingleLineMethodCalls)
				{
					node = node.WithArgumentList(OneLine(node.ArgumentList));
				}

				return node;
			}

			public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
			{
				node = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;
				if (_options.SingleLineConstructorCalls && node.ArgumentList is not null)
				{
					node = node.WithArgumentList(OneLine(node.ArgumentList));
				}

				return node;
			}

			private static ParameterListSyntax OneLine(ParameterListSyntax list)
			{
				if (list.Parameters.Count == 0)
					return list;

				var text = "(" + string.Join(", ", list.Parameters.Select(x => x.ToFullString().Trim())) + ")";
				return SyntaxFactory.ParseParameterList(text).WithLeadingTrivia(list.GetLeadingTrivia()).WithTrailingTrivia(list.GetTrailingTrivia());
			}

			private static ArgumentListSyntax OneLine(ArgumentListSyntax list)
			{
				if (list.Arguments.Count == 0)
					return list;

				var text = "(" + string.Join(", ", list.Arguments.Select(x => x.ToFullString().Trim())) + ")";
				return SyntaxFactory.ParseArgumentList(text).WithLeadingTrivia(list.GetLeadingTrivia()).WithTrailingTrivia(list.GetTrailingTrivia());
			}
		}

		private sealed record TextEdit(int Start, int Length, string Replacement);
	}
}
