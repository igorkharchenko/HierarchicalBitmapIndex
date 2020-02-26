using CSharpTest.Net.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections;

namespace HierarchicalBitmapIndex
{
	/// <summary>
	/// Bitmap index.
	/// TODO переделать типы данных для хранения ключей и значений на дженерики
	/// (сразу не сделал как нужно, потому что битовые операции над дженериками выполнять нельзя)
	/// </summary>
	class BitmapIndex<TKey, TValue>
	{
		/// <summary>
		/// Branch nodes in bitmap tree.
		/// </summary>
		private List<BranchNode<TKey, TValue>> _branchNodes = new List<BranchNode<TKey, TValue>>();

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
			foreach (BranchNode<TKey, TValue> node in _branchNodes)
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
		public void Add(TKey key, TValue value)
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
		public void Update(TKey key, TValue oldValue, TValue newValue)
		{
			_branchNodes[findIndexOfBranchNode(key)].NodeTree[key] = newValue;
		}

		/// <summary>
		/// Deletes all elements from an index with specified key.
		/// </summary>
		/// <param name="key">Key to delete.</param>
		public void Delete(TKey key)
		{
			var tree = _branchNodes[findIndexOfBranchNode(key)].NodeTree;
			TValue value;
			tree.TryRemove(key, out value);
		}

		#region Deletes an element with specified key and value (unsuppported with an index structure without duplcates)
		/*
		/// <summary>
		/// Deletes an element with specified key and value.
		/// </summary>
		/// <param name="key">Key of element.</param>
		/// <param name="value">Value of element.</param>
		public void Delete(TKey key, TValue value)
		{
			var tree = _branchNodes[findIndexOfBranchNode(key)].NodeTree;

			List<TValue> values;
			if (!tree.TryGetValue(key, out values))
			{
				values = new List<TValue>();
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
		public bool Search(TKey key, out TValue value)
		{
			BPlusTree<TKey, TValue> tree = _branchNodes[findIndexOfBranchNode(key)].NodeTree;
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
		public IEnumerable<KeyValuePair<TKey, TValue>> SearchRange(TKey start, TKey end)
		{
			var values = new List<KeyValuePair<TKey, TValue>>();

			// todo тут лучше через getHashCode сделать, потому что строки конвертацией не сравнишь
			int rangeStart = Convert.ToInt32(start);
			if (rangeStart > Convert.ToInt32(end))
			{
				throw new Exception("Range start must be greater than range end.");
			}
			if (rangeStart < 0)
			{
				throw new Exception("Range start must be greater than range end.");
			}

			int startIndex = findIndexOfBranchNode(start);
			if (startIndex < 0)
			{
				yield return new KeyValuePair<TKey, TValue>();
			}

			foreach (BranchNode<TKey, TValue> branchNode in _branchNodes.GetRange(startIndex - 1, _branchesAmount - startIndex))
			{
				values = branchNode.NodeTree.EnumerateRange(start, end).ToList();
				// @todo убрать
				break;
			}
			
			foreach (KeyValuePair<TKey, TValue> value in values)
			{
				yield return value;
			}
		}

		/// <summary>
		/// Only for benchmark purposes.
		/// Enables count of elements in B+ tree after all elements have been set to trees.
		/// </summary>
		public void EnableCountInBPlusTrees()
		{
			foreach (BranchNode<TKey, TValue> node in _branchNodes)
			{
				node.NodeTree.EnableCount();
			}
		}

		/// <summary>
		/// Only for benchmark purposes.
		/// Returns list of amounts of elements in each B+ tree.
		/// </summary>
		/// <returns></returns>
		public List<int> CountOfElementsInBPlusTrees()
		{
			List<int> amount = new List<int>();

			foreach (BranchNode<TKey, TValue> node in _branchNodes)
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
		private int findIndexOfBranchNode(TKey key)
		{
			// если ключ равен 0, то он точно располагается в самой последней ветке
			if (key.Equals(0))
			{
				return _branchesAmount - 1;
			}

			int index = 0;
			foreach (BranchNode<TKey, TValue> branchNode in _branchNodes)
			{
				var result = branchNode.Key & Convert.ToInt32(key);

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
			int bitmapSize = (Marshal.SizeOf(typeof(TKey)) * 8) - 1; // -1, потому что в int max value отсутствует нолик на крайнем левом (знаковом) бите
			int rightOffset = bitmapSize;

			for (int i = 0; i < _branchesAmount; i++)
			{
				var branchNode = new BranchNode<TKey, TValue>() { Key = 0, NodeTree = initBTree() };

				var bitmapPartSize = _bitmapPartsSizes[i];
				rightOffset -= bitmapPartSize;

				// 1. сделать операцию >> на sizeof(int) - PartSize элементов
				// 2. сделать операцию << на sizeof(int) - rightOffset

				// выставляем маску ветки
				branchNode.Key = (int.MaxValue >> (bitmapSize - bitmapPartSize) << rightOffset);

				_branchNodes.Add(branchNode);
			}
		}

		/// <summary>
		/// Initializes B+ tree.
		/// </summary>
		/// <returns></returns>
		private BPlusTree<TKey, TValue> initBTree()
		{
			return (new BPlusTreeBuilder<TKey, TValue>()).build();
		}
	}
}
