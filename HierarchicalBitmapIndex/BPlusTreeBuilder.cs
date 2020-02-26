using CSharpTest.Net.Collections;
using CSharpTest.Net.Serialization;
using System;
using System.Collections.Generic;
using System.IO;

namespace HierarchicalBitmapIndex
{
	/// <summary>
	/// Builder of B+ tree structure.
	/// </summary>
	/// <typeparam name="TKey">Type of key in B+ tree.</typeparam>
	/// <typeparam name="TValue">Type of value in B+ tree</typeparam>
	class BPlusTreeBuilder<TKey, TValue>
	{
		/// <summary>
		/// Builds an instance of B+ tree.
		/// </summary>
		/// <returns>An instance of B+ tree.</returns>
		public BPlusTree<TKey, TValue> build()
		{
			var options = new BPlusTree<TKey, TValue>.OptionsV2(getKeyTypeSerializer(typeof(TKey)), getValueTypeSerializer(typeof(TValue)));
			options.CalcBTreeOrder(sizeof(int) * 8, sizeof(int) * 8);
			options.FileBlockSize = 1024;
			options.ExistingLogAction = ExistingLogAction.Truncate;
			options.CreateFile = CreatePolicy.Always;
			// options.StorageType = StorageType.Disk;
			options.FileName = Path.GetTempFileName();

			return new BPlusTree<TKey, TValue>(options);
		}

		/// <summary>
		/// Gets type serialzer for key.
		/// </summary>
		/// <param name="type">Type.</param>
		/// <returns>Key serializer, of course.</returns>
		protected virtual ISerializer<TKey> getKeyTypeSerializer(Type type)
		{
			if (new List<string>() { "Int16", "Int32", "Int64" }.Contains(type.Name))
			{
				return (ISerializer<TKey>)PrimitiveSerializer.Int32;
			}

			if (type.Name.Equals("Byte"))
			{
				return (ISerializer<TKey>)PrimitiveSerializer.Byte;
			}

			throw new Exception("Type is not supported by this index structure. You must override this method for implementing your own type.");
		}

		/// <summary>
		/// Gets type serialzer for value.
		/// </summary>
		/// <param name="type">Type.</param>
		/// <returns>Value serializer, of course.</returns>
		protected virtual ISerializer<TValue> getValueTypeSerializer(Type type)
		{
			if (new List<string>() { "Int16", "Int32", "Int64" }.Contains(type.Name))
			{
				return (ISerializer<TValue>)PrimitiveSerializer.Int32;
			}

			if (type.Name.Equals("Byte"))
			{
				return (ISerializer<TValue>)PrimitiveSerializer.Byte;
			}

			throw new Exception("Type is not supported by this index structure. You must override this method for implementing your own type.");
		}
	}
}
