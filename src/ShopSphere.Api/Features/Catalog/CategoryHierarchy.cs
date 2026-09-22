using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Catalog;

// The category table is small, so these work on the full list in memory
// instead of recursive SQL.
public static class CategoryHierarchy
{
    public static HashSet<int> GetSelfAndDescendantIds(IEnumerable<Category> categories, int rootId)
    {
        var childrenByParent = categories.ToLookup(c => c.ParentId);
        var result = new HashSet<int>();
        var pending = new Stack<int>();
        pending.Push(rootId);

        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!result.Add(id))
                continue;

            foreach (var child in childrenByParent[id])
                pending.Push(child.Id);
        }

        return result;
    }

    public static List<Category> GetPath(IEnumerable<Category> categories, int categoryId)
    {
        var byId = categories.ToDictionary(c => c.Id);
        var path = new List<Category>();

        var current = byId.GetValueOrDefault(categoryId);
        while (current != null && path.Count < byId.Count)
        {
            path.Insert(0, current);
            current = current.ParentId.HasValue ? byId.GetValueOrDefault(current.ParentId.Value) : null;
        }

        return path;
    }
}
