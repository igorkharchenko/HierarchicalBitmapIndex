using CSharpTest.Net.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HierarchicalBitmapIndex
{
	/// <summary>
	/// Bitmap index.
	/// TODO переделать типы данных для хранения ключей и значений на дженерики
	/// (сразу не сделал как нужно, потому что битовые операции над дженериками выполнять нельзя)
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
		/// For benchmark purposes.
		/// Returns amount of operations in B+ tree in last search.
		/// </summary>
		public int AmountOfOperationsInBPlusTree;

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
			AmountOfOperationsInBPlusTree = 0;

			initIndex();
		}

		/// <summary>
		/// Destructor. Deletes memory used by the B+ tree.
		/// </summary>
		~BitmapIndex()
		{
			foreach (BranchNode node in _branchNodes)
			{
				node.NodeTree.Dispose();
			}
		}

		/// <summary>
		/// Adds a tuple to the index.
		/// 
		/// SQL equivalent of this operation:
		/// INSERT INTO table(col1, col2, ...) VALUES (1, 2, ...)
		/// </summary>
		/// <param name="key">Key to add in index.</param>
		/// <param name="value">Value to add in index.</param>
		public void Add(int key, int value)
		{
			// 1. сначала нужно найти, в какую ветку нужно добавить кортеж,
			// для этого нужно пробежаться по всем бранчам и сравнить их битовые маски
			// сравнение происходит операцией ИЛИ
			// 2. нашли номер ветки, записываем кортеж в дерево
			_branchNodes[findIndexOfBranchNode(key)].NodeTree[key] = value;
		}

		/// <summary>
		/// Updates a tuple in the index.
		/// </summary>
		/// <param name="key">Key of element.</param>
		/// <param name="oldValue">Old value.</param>
		/// <param name="newValue">New value.</param>
		public void Update(int key, int oldValue, int newValue)
		{
			_branchNodes[findIndexOfBranchNode(key)].NodeTree[key] = newValue;
		}

		/// <summary>
		/// Deletes all elements from an index with specified key.
		/// </summary>
		/// <param name="key">Key to delete.</param>
		public void Delete(int key)
		{
			var tree = _branchNodes[findIndexOfBranchNode(key)].NodeTree;
			int value;
			tree.TryRemove(key, out value);
		}

		#region Deletes an element with specified key and value (unsuppported with an index structure without duplcates)
		/*
		/// <summary>
		/// Deletes an element with specified key and value.
		/// </summary>
		/// <param name="key">Key of element.</param>
		/// <param name="value">Value of element.</param>
		public void Delete(int key, int value)
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
		*/
		#endregion

		/// <summary>
		/// Search an element in the index.
		/// </summary>
		/// <param name="key">Key to search.</param>
		/// <param name="value">Value.</param>
		/// <returns>List of tuples which key is equal to the element.</returns>
		public bool Search(int key, out int value)
		{
			BPlusTree<int, int> tree = _branchNodes[findIndexOfBranchNode(key)].NodeTree;
			bool result = tree.TryGetValue(key, out value);

			AmountOfOperationsInBPlusTree = tree.AmountOfOperations;

			return result;
		}

		/// <summary>
		/// Searches a range of values.
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="values"></param>
		/// <returns></returns>
		public bool SearchRange(int start, int end, out List<KeyValuePair<int, int>> values)
		{
			values = new List<KeyValuePair<int, int>>();

			if (start < end)
			{
				throw new Exception("Range start must be greater than range end.");
			}

			int startIndex = findIndexOfBranchNode(start);
			foreach (BranchNode branchNode in _branchNodes.GetRange(startIndex, _branchesAmount))
			{
				values.AddRange(branchNode.NodeTree.EnumerateRange(start, end).ToList());
			}

			return true;
		}

		/// <summary>
		/// For benchmark purposes only.
		/// Enables count of elements in B+ tree after all elements have been set to trees.
		/// </summary>
		public void EnableCountInBPlusTrees()
		{
			foreach (BranchNode node in _branchNodes)
			{
				node.NodeTree.EnableCount();
			}
		}

		/// <summary>
		/// For benchmark purposes only.
		/// Returns list of amounts of elements in each B+ tree.
		/// </summary>
		/// <returns></returns>
		public List<int> CountOfElementsInBPlusTrees()
		{
			List<int> amount = new List<int>();

			foreach (BranchNode node in _branchNodes)
			{
				amount.Add(node.NodeTree.Count);
			}

			return amount;
		}

		/// <summary>
		/// Finds an index of branch node in which an element is placed.
		/// </summary>
		/// <param name="key">Key to find in with branch node it is places.</param>
		/// <returns>Index of branch node.</returns>
		private int findIndexOfBranchNode(int key)
		{
			// если ключ равен 0, то он точно располагается в самой последней ветке
			if (0 == key)
			{
				return _branchesAmount - 1;
			}

			int index = 0;
			foreach (BranchNode branchNode in _branchNodes)
			{
				var result = (int)(branchNode.Key & key);

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
			int bitmapSize = (sizeof(int) * 8) - 1; // -1, потому что в int max value отсутствует нолик на числовом бите
			int rightOffset = bitmapSize;

			for (int i = 0; i < _branchesAmount; i++)
			{
				var branchNode = new BranchNode() { Key = 0, NodeTree = initBTree() };

				var bitmapPartSize = _bitmapPartsSizes[i];
				rightOffset -= bitmapPartSize;

				// 1. сделать операцию >> на sizeof(int) - PartSize элементов
				// 2. сделать операцию << на sizeof(int) - rightOffset

				// выставляем маску ветки
				branchNode.Key = (int)(int.MaxValue >> (bitmapSize - bitmapPartSize) << rightOffset);

				_branchNodes.Add(branchNode);
			}
		}

		private BPlusTree<int, int> initBTree()
		{
			return (new BPlusTreeBuilder()).build();
		}
	}
}
