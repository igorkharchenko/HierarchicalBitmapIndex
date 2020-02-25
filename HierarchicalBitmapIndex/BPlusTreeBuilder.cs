using CSharpTest.Net.Collections;
using CSharpTest.Net.Serialization;
using System.IO;

namespace HierarchicalBitmapIndex
{
	class BPlusTreeBuilder
	{
		/// <summary>
		/// Builds an instance of B+ tree.
		/// </summary>
		/// <returns>An instance of B+ tree.</returns>
		public BPlusTree<int, int> build()
		{
			var options = new BPlusTree<int, int>.OptionsV2(PrimitiveSerializer.Int32, PrimitiveSerializer.Int32);
			options.CalcBTreeOrder(sizeof(int) * 8, sizeof(int) * 8);
			options.FileBlockSize = 1024;
			options.ExistingLogAction = ExistingLogAction.Truncate;
			options.CreateFile = CreatePolicy.Always;
			// options.StorageType = StorageType.Disk;
			options.FileName = Path.GetTempFileName();

			return new BPlusTree<int, int>(options);
		}
	}
}
