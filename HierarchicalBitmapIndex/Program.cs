using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CSharpTest.Net.Collections;
using CSharpTest.Net.Serialization;
using System.IO;

namespace HierarchicalBitmapIndex
{
	/*
	class Node
	{
		public int Key;

		public int RowID;
	}
	*/

	/// <summary>
	/// Branch node.
	/// </summary>
	class BranchNode
	{
		/// <summary>
		/// Bitmap of branch node.
		/// </summary>
		public byte Key;

		/// <summary>
		/// Binary serach tree of nodes.
		/// </summary>
		public BPlusTree<byte, List<int>> NodeTree;
	}

	/// <summary>
	/// Bitmap index.
	/// </summary>
	class BitmapIndex
	{
		/// <summary>
		/// Branch nodes in bitmap tree.
		/// </summary>
		private List<BranchNode> _branchNodes = new List<BranchNode>();

		/// <summary>
		/// Amount of branches in tree.
		/// </summary>
		private int _branchesAmount;

		/// <summary>
		/// Sizes of bitmap parts.
		/// </summary>
		private int[] _bitmapPartsSizes;

		/// <summary>
		/// Index constructor.
		/// </summary>
		/// <param name="branchesAmount">Amount of branches.</param>
		/// <param name="bitmapPartsSizes">Sizes of bitmap parts.</param>
		/// <param name="tuples">List of tuples to initialize</param>
		public BitmapIndex(int[] bitmapPartsSizes)
		{
			foreach (int bitmapPartsSize in bitmapPartsSizes)
			{
				if (bitmapPartsSize <= 0)
				{
					throw new ArgumentException("Size of branch bitmap part cannot be less than 1.");
				}
			}

			_branchesAmount = bitmapPartsSizes.Length;
			_bitmapPartsSizes = bitmapPartsSizes;

			initIndex();
		}

		/// <summary>
		/// Adds a tuple to the index.
		/// 
		/// SQL equivalent of this operation:
		/// INSERT INTO table(col1, col2, ...) VALUES (1, 2, ...)
		/// </summary>
		/// <param name="key">Key to add in index.</param>
		/// <param name="value">Value to add in index.</param>
		public void Add(byte key, int value)
		{
			// 1. сначала нужно найти, в какую ветку нужно добавить кортеж,
			// для этого нужно пробежаться по всем бранчам и сравнить их битовые маски
			// сравнение происходит операцией ИЛИ
			// 2. нашли номер ветки, записываем кортеж в дерево
			var tree = _branchNodes[findIndexOfBranchNode(key)].NodeTree;

			List<int> values;
			if (! tree.TryGetValue(key, out values))
			{
				values = new List<int>();
			}

			// ищем элемент, если его нет - добавляем
			if (-1 == values.LastIndexOf(value))
			{
				values.Add(value);
				tree[key] = values;
			}
		}
		
		/// <summary>
		/// Updates a tuple in the index.
		/// </summary>
		/// <param name="key">Key of element.</param>
		/// <param name="oldValue">Old value.</param>
		/// <param name="newValue">New value.</param>
		public void Update(byte key, int oldValue, int newValue)
		{
			var tree = _branchNodes[findIndexOfBranchNode(key)].NodeTree;

			List<int> values;
			if (!tree.TryGetValue(key, out values))
			{
				values = new List<int>();
			}

			// ищем элемент, если он есть - удаляем старый
			if (-1 != values.LastIndexOf(oldValue))
			{
				values.Remove(oldValue);
			}

			// добавляем элемент в список
			values.Add(newValue);
			tree[key] = values;
		}

		/// <summary>
		/// Deletes all elements from an index with specified key.
		/// </summary>
		/// <param name="key">Key to delete.</param>
		public void Delete(byte key)
		{
			_branchNodes[findIndexOfBranchNode(key)].NodeTree[key] = new List<int>();
		}
		
		/// <summary>
		/// Deletes an element with specified key and value.
		/// </summary>
		/// <param name="key">Key of element.</param>
		/// <param name="value">Value of element.</param>
		public void Delete(byte key, int value)
		{
			var tree = _branchNodes[findIndexOfBranchNode(key)].NodeTree;

			List<int> values;
			if (!tree.TryGetValue(key, out values))
			{
				values = new List<int>();
			}

			// ищем элемент, если он есть - удаляем
			if (-1 != values.LastIndexOf(value))
			{
				values.Remove(value);
				tree[key] = values;
			}
		}

		/// <summary>
		/// Search an element in the index.
		/// </summary>
		/// <param name="key">Key to search.</param>
		/// <returns>List of tuples which key is equal to the element.</returns>
		public List<int> Search(byte key)
		{
			List<int> values;
			_branchNodes[findIndexOfBranchNode(key)].NodeTree.TryGetValue(key, out values);

			return values;
		}

		/// <summary>
		/// Finds an index of branch node in which an element is placed.
		/// </summary>
		/// <param name="key">Key to find in with branch node it is places.</param>
		/// <returns>Index of branch node.</returns>
		private int findIndexOfBranchNode(byte key)
		{
			// если ключ равен 0, то он точно располагается в самой последней ветке
			if (0 == key)
			{
				return _branchesAmount - 1;
			}

			int index = 0;
			foreach (BranchNode branchNode in _branchNodes)
			{
				var result = (byte)(branchNode.Key & key);

				// если элемент не проходит по маске, то элемента в ветке точно нет
				if (0 == result)
				{
					index++;
					continue;
				}

				// иначе элемент точно есть
				return index;
			}

			throw new Exception("Error during find index of branch node.");
		}

		/// <summary>
		/// Initializes an index.
		/// </summary>
		private void initIndex()
		{
			int bitmapSize = sizeof(byte) * 8;
			int rightOffset = bitmapSize;

			for (int i = 0; i < _branchesAmount; i++)
			{
				var branchNode = new BranchNode() { Key = 0, NodeTree = initBTree() };

				var bitmapPartSize = _bitmapPartsSizes[i];
				rightOffset -= bitmapPartSize;

				// 1. сделать операцию >> на sizeof(byte) - PartSize элементов
				// 2. сделать операцию << на sizeof(byte) - rightOffset

				// выставляем маску ветки
				branchNode.Key = (byte)(byte.MaxValue >> (bitmapSize - bitmapPartSize) << rightOffset);

				_branchNodes.Add(branchNode);
			}
		}

		private BPlusTree<byte, List<int>> initBTree()
		{
			var options = new BPlusTree<byte, List<int>>.OptionsV2(PrimitiveSerializer.Byte, PrimitiveSerializer.Int32);
			options.CalcBTreeOrder(sizeof(byte) * 8, sizeof(int) * 8);
			options.CreateFile = CreatePolicy.Always;
			options.FileName = Path.GetTempFileName();
			
			return new BPlusTree<byte, List<int>>(options);

			/*
			 options.CreateFile = CreatePolicy.Never;
			using (var tree = new BPlusTree<string, DateTime>(options))
			{
				var tempDir = new DirectoryInfo(Path.GetTempPath());
				foreach (var file in tempDir.GetFiles("*", SearchOption.AllDirectories))
				{
					DateTime cmpDate;
					if (!tree.TryGetValue(file.FullName, out cmpDate))
						Console.WriteLine("New file: {0}", file.FullName);
					else if (cmpDate != file.LastWriteTimeUtc)
						Console.WriteLine("Modified: {0}", file.FullName);
					tree.Remove(file.FullName);
				}
				foreach (var item in tree)
				{
					Console.WriteLine("Removed: {0}", item.Key);
				}
			}
			*/
		}
	}

	class Program
	{
		static void Main(string[] args)
		{
			#region learning dictionary structure
			/*
			List<KeyValuePair<byte, int>> nodes = new List<KeyValuePair<byte, int>>();
			nodes.Add(new KeyValuePair<byte, int>(1,   1));
			nodes.Add(new KeyValuePair<byte, int>(1,   2));
			nodes.Add(new KeyValuePair<byte, int>(1,   3));
			nodes.Add(new KeyValuePair<byte, int>(104, 4));
			nodes.Add(new KeyValuePair<byte, int>(12,  5));
			
			var elements = nodes.FindAll(x => x.Key >= 1 && x.Key <= 12 && x.Value >= 2);

			foreach (KeyValuePair<byte, int> element in elements)
			{
				Console.WriteLine(string.Format("key = {0}, value = {1}", element.Key, element.Value) + Environment.NewLine);
			}

			Console.Read();
			Environment.Exit(0);
			*/

			/*
			var btree = new BTreeDictionary<byte, int>();
			
			btree.Add(5, 1);
			btree.Add(7, 2);
			btree.Add(6, 3);
			btree.Add(1, 4);
			btree.Add(1, 5);
			btree.Add(1, 6);

			int value;
			var result = btree.TryGetValue(1, out value);

			Console.WriteLine(string.Format("Element with key = {0}: {0}, result = {1}", value, result) + Environment.NewLine);
			Console.WriteLine(string.Format("Element exists? {0}", btree.ContainsKey(1)));

			Console.Read();
			Environment.Exit(0);
			*/
			#endregion

			#region try to test HBI with only ten elements, hope that it will be successful
			/*
			var indexTree = new BitmapIndex(new int[] { 2, 3, 3 });

			List<Tuple<byte, int>> tableWithTuples = new List<Tuple<byte, int>>();
			tableWithTuples.Add(new Tuple<byte, int>(4,   1));
			tableWithTuples.Add(new Tuple<byte, int>(105, 2));
			tableWithTuples.Add(new Tuple<byte, int>(17,  3));
			tableWithTuples.Add(new Tuple<byte, int>(203, 4));
			tableWithTuples.Add(new Tuple<byte, int>(47,  5));

			foreach (Tuple<byte, int> tuple in tableWithTuples)
			{
				indexTree.Add(tuple);
			}

			List<Tuple<byte, int>> searchResults = indexTree.Search(4);

			Console.ReadLine();

			Environment.Exit(0);
			*/
			#endregion

			#region HBI benchmarking
			
			List<Tuple<byte, int>> table = generateTuples(100);

			var searchItem = new { ItemKey = table[0].Item1, ItemValue = table[0].Item2 };

			Stopwatch stopwatch = new Stopwatch();

			Process process = Process.GetCurrentProcess();
			process.ProcessorAffinity = new IntPtr(2);
			process.PriorityClass = ProcessPriorityClass.High;
			Thread.CurrentThread.Priority = ThreadPriority.Highest;
			
			// time (ms) to warmup for stabilize CPU cache and pipeline
			int warmupSeconds = 1200;

			// ==============================================
			Console.WriteLine("Full table scan imitation...");

			stopwatch.Reset();
			stopwatch.Start();
			while (stopwatch.ElapsedMilliseconds < warmupSeconds)
			{
				searchItemInTable(table, searchItem.ItemKey, -3841214);
			}
			stopwatch.Stop();

			long seconds = 0;
			for (int repeats = 0; repeats < 10; repeats++)
			{
				stopwatch.Reset();
				stopwatch.Start();

				searchItemInTable(table, searchItem.ItemKey, -3841214);

				stopwatch.Stop();
				Console.WriteLine("Elapsed: {0} ms", stopwatch.ElapsedMilliseconds);
				seconds += stopwatch.ElapsedMilliseconds;
			}
			Console.WriteLine(Environment.NewLine + "Total elapsed: {0} ms", seconds / 10);
			Console.WriteLine(Environment.NewLine);

			// ==============================================
			Console.WriteLine("Adding items to HBI...");

			var tree = new BitmapIndex(new int[] { 2, 3, 3 });

			stopwatch.Reset();
			stopwatch.Start();
			while (stopwatch.ElapsedMilliseconds < warmupSeconds)
			{
				foreach (Tuple<byte, int> tuple in table)
				{
					tree.Add(tuple);
				}

				foreach (Tuple<byte, int> tuple in table)
				{
					tree.Delete(tuple.Item1);
				}
			}
			stopwatch.Stop();

			seconds = 0;
			for (int repeats = 0; repeats < 10; repeats++)
			{
				stopwatch.Reset();
				stopwatch.Start();

				foreach (Tuple<byte, int> tuple in table)
				{
					tree.Add(tuple);
				}

				stopwatch.Stop();
				Console.WriteLine("Elapsed: {0} ms", stopwatch.ElapsedMilliseconds);
				seconds += stopwatch.ElapsedMilliseconds;
			}
			Console.WriteLine(Environment.NewLine + "Total elapsed: {0} ms", seconds / 10);
			Console.WriteLine(Environment.NewLine);


			// ==============================================
			Console.WriteLine("Search an item in HBI...");

			stopwatch.Reset();
			stopwatch.Start();
			while (stopwatch.ElapsedMilliseconds < warmupSeconds)
			{
				tree.Search(searchItem.ItemKey);
			}
			stopwatch.Stop();

			seconds = 0;
			for (int repeats = 0; repeats < 10; repeats++)
			{
				stopwatch.Reset();
				stopwatch.Start();

				tree.Search(searchItem.ItemKey);
				
				stopwatch.Stop();
				Console.WriteLine("Elapsed: {0} ms", stopwatch.ElapsedMilliseconds);
				seconds += stopwatch.ElapsedMilliseconds;
			}
			Console.WriteLine(Environment.NewLine + "Total elapsed: {0} ms", seconds / 10);
			Console.WriteLine(Environment.NewLine);


			// ==============================================
			Console.WriteLine("Removing items from HBI...");

			stopwatch.Reset();
			stopwatch.Start();
			while (stopwatch.ElapsedMilliseconds < warmupSeconds)
			{
				foreach (Tuple<byte, int> tuple in table)
				{
					tree.Delete(tuple.Item1);
				}

				foreach (Tuple<byte, int> tuple in table)
				{
					tree.Add(tuple);
				}
			}
			stopwatch.Stop();

			seconds = 0;
			for (int repeats = 0; repeats < 10; repeats++)
			{
				stopwatch.Reset();
				stopwatch.Start();

				foreach (Tuple<byte, int> tuple in table)
				{
					tree.Delete(tuple.Item1);
				}

				stopwatch.Stop();
				Console.WriteLine("Elapsed: {0} ms", stopwatch.ElapsedMilliseconds);
				seconds += stopwatch.ElapsedMilliseconds;
			}
			Console.WriteLine(Environment.NewLine + "Total elapsed: {0} ms", seconds / 10);
			Console.WriteLine(Environment.NewLine);


			#endregion

			Console.Read();
		}

		/// <summary>
		/// Search an item in tuples. 
		/// This is an imitation of full table scan operation.
		/// </summary>
		/// <param name="tuples">Tuples to search in.</param>
		/// <param name="key">A key to search.</param>
		/// <param name="item">An item to search.</param>
		/// <returns></returns>
		static bool searchItemInTable(List<Tuple<byte, int>> tuples, byte key, int item)
		{
			foreach (Tuple<byte, int> tuple in tuples)
			{
				if (tuple.Item1 == key && tuple.Item2 == item)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Generate tuples to insert to an hierarchical bitmap index.
		/// </summary>
		/// <param name="keyTypeSize">Size of type of key.</param>
		/// <returns>List of generated tuples.</returns>
		static List<Tuple<byte, int>> generateTuples(int tuplesAmount)
		{
			List<Tuple<byte, int>> tuples = new List<Tuple<byte, int>>();

			Random random = new Random();

			for (int i = 0; i < tuplesAmount; i++)
			{
				byte[] bytes = new byte[1];
				random.NextBytes(bytes);
				tuples.Add(new Tuple<byte, int>((byte)i, i + 1));
			}

			return tuples;
		}
	}
}
