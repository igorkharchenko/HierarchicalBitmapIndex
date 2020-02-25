using CSharpTest.Net.Collections;

namespace HierarchicalBitmapIndex
{
	/// <summary>
	/// Branch node.
	/// </summary>
	class BranchNode
	{
		/// <summary>
		/// Bitmap of branch node.
		/// </summary>
		public int Key;

		/// <summary>
		/// Binary serach tree of nodes.
		/// </summary>
		public BPlusTree<int, int> NodeTree;
	}
}
