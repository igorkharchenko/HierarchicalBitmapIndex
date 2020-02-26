using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace HierarchicalBitmapIndex
{
	struct SearchedValues
	{
		public int WorstCase;

		public int AverageCase;

		public int BestCase;

		public SearchedValues(int worstCase, int averageCase, int bestCase)
		{
			WorstCase = worstCase;
			AverageCase = averageCase;
			BestCase = bestCase;
		}
	}

	class Program
	{
		/// <summary>
		/// I'm just trying to run benchmarks on various configurations.
		/// </summary>
		/// <returns>Bitmap index configuration</returns>
		private static int[] GetHBIConfiguration()
		{
			// return new int[] { 6, 4, 21 };
			// 0111 1110 | 0000 0000 | 0000 0000 | 0000 0000 = 2113929216 (6 единиц)
			// 0000 0001 | 1110 0000 | 0000 0000 | 0000 0000 = 31457280   (4 единицы)
			// 0000 0000 | 0001 1111 | 1111 1111 | 1111 1111 = 2097151    (21 единица)

			// return new int[] { 6, 7, 18 };
			// 0111 1110 | 0000 0000 | 0000 0000 | 0000 0000 = 2113929216 (6 единиц)
			// 0000 0001 | 1111 1100 | 0000 0000 | 0000 0000 = 33292288   (7 единиц)
			// 0000 0000 | 0000 0011 | 1111 1111 | 1111 1111 = 16383      (18 единиц)

			return new int[] { 2, 1, 28 };
			// 0110 0000 | 0000 0000 | 0000 0000 | 0000 0000 = 1610612736 (2 единицы)
			// 0001 0000 | 0000 0000 | 0000 0000 | 0000 0000 = 268435456  (1 единица)
			// 0000 1111 | 1111 1111 | 1111 1111 | 1111 1111 = 268435455  (28 единиц)
		}

		static void Main(string[] args)
		{
			// необходимо составить бенчмарк для получения количества операций ввода-вывода:
			// 1. для нахождения одного элемента;
			// 2. для нахождения нескольких элементов.
			
			var tree = new BitmapIndex<int, int>(GetHBIConfiguration());

			int amountOfTuples = 50000;
			// values to search: for worst, average and best cases
			var searchedValues = new SearchedValues(worstCase: 528216031, averageCase: 2087391, bestCase: 58);

			List<Tuple<int, int>> table = generateTuples(amountOfTuples);

			foreach (Tuple<int, int> tuple in table)
			{
				try
				{
					tree.Add(tuple.Item1, tuple.Item2);
				}
				catch (CSharpTest.Net.DuplicateKeyException)
				{
					continue;
				}
			}
			tree.EnableCountInBPlusTrees();
			StringBuilder builder = new StringBuilder();
			foreach (int amount in tree.CountOfElementsInBPlusTrees())
			{
				builder.Append(string.Format("{0}, ", amount));
			}
			Console.WriteLine("Amount of elements in all trees: {0}", builder.ToString());

			Stopwatch stopwatch = new Stopwatch();

			Process process = Process.GetCurrentProcess();
			process.ProcessorAffinity = new IntPtr(2);
			process.PriorityClass = ProcessPriorityClass.High;
			Thread.CurrentThread.Priority = ThreadPriority.Highest;

			// time (ms) to warmup for stabilize CPU cache and pipeline
			int warmupSeconds = 1200;

			#region Full table scan
			// ==============================================
			Console.WriteLine("Full table scan imitation...");

			stopwatch.Reset();
			stopwatch.Start();
			while (stopwatch.ElapsedMilliseconds < warmupSeconds)
			{
				searchItemInTable(table, 100500, -3841214);
			}
			stopwatch.Stop();

			long seconds = 0;
			for (int repeats = 0; repeats < 10; repeats++)
			{
				stopwatch.Reset();
				stopwatch.Start();

				searchItemInTable(table, searchedValues.WorstCase, -3841214);

				stopwatch.Stop();
				Console.WriteLine("Elapsed: {0} ms", stopwatch.ElapsedMilliseconds);
				seconds += stopwatch.ElapsedMilliseconds;
			}
			Console.WriteLine(Environment.NewLine + "Total elapsed: {0} ms", seconds / 10);
			Console.WriteLine(Environment.NewLine);

			#endregion

			#region Search one item
			// ===================================================
			Console.WriteLine("Search an item in HBI...");

			stopwatch.Reset();
			stopwatch.Start();
			int value;
			while (stopwatch.ElapsedMilliseconds < warmupSeconds)
			{
				tree.Search(searchedValues.WorstCase, out value);
				tree.Search(searchedValues.AverageCase, out value);
				tree.Search(searchedValues.BestCase, out value);
			}
			stopwatch.Stop();

			int[] cases = new int[3];
			seconds = 0;
			for (int repeats = 0; repeats < 10; repeats++)
			{
				stopwatch.Reset();
				stopwatch.Start();

				tree.Search(searchedValues.WorstCase, out value);
				cases[0] = tree.AmountOfOperationsInBPlusTree;

				tree.Search(searchedValues.AverageCase, out value);
				cases[1] = tree.AmountOfOperationsInBPlusTree;

				tree.Search(searchedValues.BestCase, out value);
				cases[2] = tree.AmountOfOperationsInBPlusTree;

				stopwatch.Stop();
				Console.WriteLine(
					"Elapsed: {0} ms | Amount of operations: worst = {1}, best = {3}, average = {2}", 
					stopwatch.ElapsedMilliseconds, 
					cases[0], cases[1], cases[2]
				);
				seconds += stopwatch.ElapsedMilliseconds;
			}
			cases = new int[3];
			Console.WriteLine(Environment.NewLine + "Total elapsed: {0} ms", seconds / 10);
			Console.WriteLine(Environment.NewLine);

			// ==================================================
			Console.WriteLine("Search an item in B+ tree...");

			var bPlusTree = (new BPlusTreeBuilder<int, int>()).build();

			foreach (Tuple<int, int> tuple in table)
			{
				try
				{
					bPlusTree.Add(tuple.Item1, tuple.Item2);
				}
				catch (CSharpTest.Net.DuplicateKeyException)
				{
					continue;
				}
			}
			bPlusTree.EnableCount();
			Console.WriteLine("Amount of elements in tree: {0}", bPlusTree.Count);
			
			stopwatch.Reset();
			stopwatch.Start();
			
			while (stopwatch.ElapsedMilliseconds < warmupSeconds)
			{
				bPlusTree.TryGetValue(searchedValues.WorstCase, out value);
				bPlusTree.TryGetValue(searchedValues.AverageCase, out value);
				bPlusTree.TryGetValue(searchedValues.BestCase, out value);
			}
			stopwatch.Stop();

			seconds = 0;
			for (int repeats = 0; repeats < 10; repeats++)
			{
				stopwatch.Reset();
				stopwatch.Start();

				tree.Search(searchedValues.WorstCase, out value);
				cases[0] = bPlusTree.AmountOfOperations;
				
				stopwatch.Stop();
				Console.WriteLine(
					"Elapsed: {0} ms | Amount of operations: worst {1}",
					stopwatch.ElapsedMilliseconds,
					cases[0]
				);
				seconds += stopwatch.ElapsedMilliseconds;
			}
			cases = new int[3];
			Console.WriteLine(Environment.NewLine + "Total elapsed: {0} ms", seconds / 10);
			Console.WriteLine(Environment.NewLine);

			#endregion

			#region Search range of items
			// ===================================================
			Console.WriteLine("Search range of items in HBI...");

			stopwatch.Reset();
			stopwatch.Start();

			List<KeyValuePair<int, int>> values;
			while (stopwatch.ElapsedMilliseconds < warmupSeconds)
			{
				tree.SearchRange(1, (Int32.MaxValue - 1), out values);
			}
			stopwatch.Stop();

			values = new List<KeyValuePair<int, int>>();
			cases = new int[3];
			
			seconds = 0;
			for (int repeats = 0; repeats < 10; repeats++)
			{
				stopwatch.Reset();
				stopwatch.Start();

				tree.SearchRange(1, (Int32.MaxValue - 1), out values);
				cases[0] = tree.AmountOfOperationsInBPlusTree;

				stopwatch.Stop();
				Console.WriteLine(
					"Elapsed: {0} ms | Amount of operations: {1}",
					stopwatch.ElapsedMilliseconds,
					cases[0]
				);
				seconds += stopwatch.ElapsedMilliseconds;
			}
			cases = new int[3];
			Console.WriteLine(Environment.NewLine + "Total elapsed: {0} ms", seconds / 10);
			Console.WriteLine(Environment.NewLine);

			// ==================================================
			Console.WriteLine("Search range of items in B+ tree...");
			
			stopwatch.Reset();
			stopwatch.Start();

			while (stopwatch.ElapsedMilliseconds < warmupSeconds)
			{
				bPlusTree.EnumerateRange(1, (int.MaxValue - 1));
			}
			stopwatch.Stop();

			seconds = 0;
			for (int repeats = 0; repeats < 10; repeats++)
			{
				stopwatch.Reset();
				stopwatch.Start();

				bPlusTree.EnumerateRange(1, (int.MaxValue - 1));
				cases[0] = bPlusTree.AmountOfOperations;
				
				stopwatch.Stop();
				Console.WriteLine(
					"Elapsed: {0} ms | Amount of operations: worst {1}",
					stopwatch.ElapsedMilliseconds,
					cases[0]
				);
				seconds += stopwatch.ElapsedMilliseconds;
			}
			cases = new int[3];
			Console.WriteLine(Environment.NewLine + "Total elapsed: {0} ms", seconds / 10);
			Console.WriteLine(Environment.NewLine);

			#endregion

			Console.ReadLine();

			Environment.Exit(0);
		}

		#region learning dictionary structure
		static void learningDictionaryStructure()
		{
			var searchedValues = new { WorstCase = 528216031, AverageCase = 2087391, BestCase = 58 };

			List<Tuple<int, int>> table = new List<Tuple<int, int>>();
			table.Add(new Tuple<int, int>(1, 91));
			table.Add(new Tuple<int, int>(2, 24));
			table.Add(new Tuple<int, int>(3, 31));
			table.Add(new Tuple<int, int>(4, 45));
			table.Add(new Tuple<int, int>(5, 100500));

			var bPlusTree = (new BPlusTreeBuilder<int, int>()).build();

			foreach (Tuple<int, int> tuple in table)
			{
				try
				{
					bPlusTree.Add(tuple.Item1, tuple.Item2);
				}
				catch (CSharpTest.Net.DuplicateKeyException)
				{
					continue;
				}
			}
			bPlusTree.EnableCount();
			Console.WriteLine("Amount of elements in tree: {0}", bPlusTree.Count);

			List<KeyValuePair<int, int>> items = new List<KeyValuePair<int, int>>();
			foreach (KeyValuePair<int, int> item in bPlusTree.EnumerateRange(1, 4))
			{
				items.Add(item);
			}

			foreach (KeyValuePair<int, int> item in items)
			{
				Console.WriteLine("Key = {0}, value = {1}", item.Key, item.Value);
			}

			Console.Read();
			Environment.Exit(0);
		}
		#endregion

		#region HBI benchmarking
		static void HBIBenchmarking()
		{
			List<Tuple<int, int>> table = generateTuples(4000);

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

			// 0111 1110 | 0000 0000 | 0000 0000 | 0000 0000 = 2113929216 (6 единиц)
			// 0000 0001 | 1111 1100 | 0000 0000 | 0000 0000 = 33292288   (7 единиц)
			// 0000 0000 | 0000 0011 | 1111 1111 | 1100 0000 = 262080     (12 единиц)
			// 0000 0000 | 0000 0000 | 0000 0000 | 0011 1111 = 63         (6 единиц)
			var tree = new BitmapIndex<int, int>(new int[] { 6, 7, 12, 6 });

			stopwatch.Reset();
			stopwatch.Start();
			while (stopwatch.ElapsedMilliseconds < warmupSeconds)
			{
				foreach (Tuple<int, int> tuple in table)
				{
					tree.Add(tuple.Item1, tuple.Item2);
				}

				foreach (Tuple<int, int> tuple in table)
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

				foreach (Tuple<int, int> tuple in table)
				{
					tree.Add(tuple.Item1, tuple.Item2);
				}

				stopwatch.Stop();
				Console.WriteLine("Elapsed: {0} ms", stopwatch.ElapsedMilliseconds);
				seconds += stopwatch.ElapsedMilliseconds;

				foreach (Tuple<int, int> tuple in table)
				{
					tree.Delete(tuple.Item1);
				}
			}
			Console.WriteLine(Environment.NewLine + "Total elapsed: {0} ms", seconds / 10);
			Console.WriteLine(Environment.NewLine);


			// ==============================================
			Console.WriteLine("Search an item in HBI...");

			stopwatch.Reset();
			stopwatch.Start();
			int value;
			while (stopwatch.ElapsedMilliseconds < warmupSeconds)
			{
				tree.Search(searchItem.ItemKey, out value);
			}
			stopwatch.Stop();

			seconds = 0;
			for (int repeats = 0; repeats < 10; repeats++)
			{
				stopwatch.Reset();
				stopwatch.Start();

				tree.Search(searchItem.ItemKey, out value);
				
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
				foreach (Tuple<int, int> tuple in table)
				{
					tree.Delete(tuple.Item1);
				}

				foreach (Tuple<int, int> tuple in table)
				{
					tree.Add(tuple.Item1, tuple.Item2);
				}
			}
			stopwatch.Stop();

			seconds = 0;
			for (int repeats = 0; repeats < 10; repeats++)
			{
				stopwatch.Reset();
				stopwatch.Start();

				foreach (Tuple<int, int> tuple in table)
				{
					tree.Delete(tuple.Item1);
				}

				stopwatch.Stop();
				Console.WriteLine("Elapsed: {0} ms", stopwatch.ElapsedMilliseconds);
				seconds += stopwatch.ElapsedMilliseconds;
			}
			Console.WriteLine(Environment.NewLine + "Total elapsed: {0} ms", seconds / 10);
			Console.WriteLine(Environment.NewLine);
		}

		#endregion

		/// <summary>
		/// Search an item in tuples. 
		/// This is an imitation of full table scan operation.
		/// </summary>
		/// <param name="tuples">Tuples to search in.</param>
		/// <param name="key">A key to search.</param>
		/// <param name="item">An item to search.</param>
		/// <returns></returns>
		static bool searchItemInTable(List<Tuple<int, int>> tuples, int key, int item)
		{
			return tuples.BinarySearch(new Tuple<int, int>(key, item)) >= 0;
		}

		/// <summary>
		/// Generate tuples to insert to an hierarchical bitmap index.
		/// </summary>
		/// <param name="keyTypeSize">Size of type of key.</param>
		/// <returns>List of generated tuples.</returns>
		static List<Tuple<int, int>> generateTuples(int tuplesAmount)
		{
			List<Tuple<int, int>> tuples = new List<Tuple<int, int>>();

			Random random = new Random();

			int maxValue = (int.MaxValue - 1) / 3;
			//  int maxValue = (int.MaxValue - 1);

			for (int i = 0; i < tuplesAmount; i++)
			{
				tuples.Add(new Tuple<int, int>( random.Next(maxValue), i ));
			}

			return tuples;
		}
	}
}
