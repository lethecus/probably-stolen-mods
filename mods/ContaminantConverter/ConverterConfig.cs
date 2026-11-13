namespace ContaminantConverter
{
	public static class ConverterConfig
	{
		public const string ItemId = "contaminant_converter";
		public const string Name = "Contaminant Converter";

		public const int PowerPerPass = 10;
		public const int BaseVolumePerPass = 250000; // 250 mL in liquid parts
		public const long UnitValue = 600L;

		public const int ShapeWidth = 3;
		public static readonly byte[] ShapeData = { 1, 1, 1, 1, 1, 1, 1, 1, 1 };

		public const string ModuleShape = "10000010001000001110000010001000001";

		public static readonly string[] AllowedModuleTypes =
		{
			"MODULE_TYPE_UNIVERSAL",
			"MODULE_TYPE_CHEM"
		};

		// Source contaminant -> converted product. The first entry has a
		// selectable product (acid or zyanide).
		public static readonly string[] Contaminants =
		{
			"chemical_contaminant",
			"organic_waste",
			"microbe",
			"mineral",
			"heavy_metal",
			"physical_contaminant"
		};

		public static readonly string[] Products =
		{
			"acid",
			"base",
			"contraceptive",
			"methylol",
			"lipobond_agent",
			"reducing_agent"
		};

		public const string AltChemicalProduct = "zyanide";

		public const string TagSelected = "CC_SELECTED";
		public const string TagChemical = "CC_CHEM";
	}
}
