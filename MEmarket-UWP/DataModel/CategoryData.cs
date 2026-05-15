namespace MEmarket_UWP.DataModel
{
    public class CategoryData
    {
        public string Id { get; set; }
        public string Key { get; set; } // Original category key from JSON
        public string Name { get; set; } // Display name
        public string IconGlyph { get; set; } // Segoe MDL2 Assets glyph for category

        public override string ToString()
        {
            return this.Name;
        }
    }
}
