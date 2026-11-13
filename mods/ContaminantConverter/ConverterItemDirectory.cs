using System;

namespace ContaminantConverter
{
	public class ConverterItemDirectory : ItemDirectory
	{
		protected override void InitDirectory()
		{
			Add(ConverterConfig.ItemId, MachineContaminantConverter.Create);
		}
	}
}
