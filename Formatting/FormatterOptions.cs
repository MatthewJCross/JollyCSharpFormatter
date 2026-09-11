namespace JollyCSharpFormatter.Formatting
{
    public sealed class FormatterOptions
    {
        public bool SingleLineMethodDeclarations { get; set; } = true;
        public bool SingleLineConstructorDeclarations { get; set; } = true;
        public bool SingleLineMethodCalls { get; set; } = true;
        public bool SingleLineConstructorCalls { get; set; } = true;
        public bool SingleLineConditions { get; set; } = true;
        public bool SingleLineRecordDeclarations { get; set; } = true;
        public bool ExpandBlocks { get; set; } = true;
        public bool UseTabs { get; set; } = false;
        public bool BlankLineAfterBlocks { get; set; } = true;
        public bool BlankLineAfterControlStatements { get; set; } = true;
        public bool BlankLineBetweenMembers { get; set; } = true;
    }
}