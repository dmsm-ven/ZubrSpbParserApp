namespace ZubrSpbParserApp.Model
{
    public class Characteristic
    {
        public string Group { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{Group} | {Name}: {Value}";
        }
    }
}
