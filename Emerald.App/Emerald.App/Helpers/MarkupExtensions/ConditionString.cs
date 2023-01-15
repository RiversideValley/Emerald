namespace Emerald.WinUI.Helpers
{
    public class ConditionString
    {
        public string TrueString { get; set; }

        public string FalseString { get; set; }

        public bool Condition { get; set; }

        public string Result
            => Condition ? TrueString : FalseString;

        public override string ToString()
            => Result;
    }
}
