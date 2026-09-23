interface TreeNode<T> {
  children: T[];
}

export interface FlatNode<T> {
  node: T;
  depth: number;
}

// Turns a nested tree into a list with depth, e.g. for indented selects and tables
export function flattenTree<T extends TreeNode<T>>(nodes: T[], depth = 0): FlatNode<T>[] {
  return nodes.flatMap(node => [{ node, depth }, ...flattenTree(node.children, depth + 1)]);
}
