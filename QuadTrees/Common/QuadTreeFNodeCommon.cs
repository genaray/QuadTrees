﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using QuadTrees.Helper;

namespace QuadTrees.Common
{
    public abstract class QuadTreeFNodeCommon
    {
        // How many objects can exist in a QuadTree before it sub divides itself
        public const int MaxObjectsPerNode = 10;//scales up to about 16 on removal
        public const int MaxOptimizeDeletionReAdd = 22;
        public static float ReBalanceOffset = 0.2f;
        public const int MinBalance = 256;

        protected RectangleF Rect; // The area this QuadTree represents

        /// <summary>
        /// The area this QuadTree represents.
        /// </summary>
        internal virtual RectangleF QuadRect
        {
            get { return Rect; }
        }
    }

    public abstract class QuadTreeFNodeCommon<T, TNode, TQuery> : QuadTreeFNodeCommon
        where TNode : QuadTreeFNodeCommon<T, TNode, TQuery>
    {
        #region Private Members

        private QuadTreeObject<T, TNode>[] _objects = null;
        private int _objectCount = 0;

        private TNode _parent = null; // The parent of this quad

        private TNode _childTl = null; // Top Left Child
        private TNode _childTr = null; // Top Right Child
        private TNode _childBl = null; // Bottom Left Child
        private TNode _childBr = null; // Bottom Right Child

        #endregion

        #region Public Properties

        public PointF CenterPoint
        {
            get { return _childBr.Rect.Location; }
        }

        /// <summary>
        /// How many total objects are contained within this QuadTree (ie, includes children)
        /// </summary>
        public int Count
        {
            get
            {
                int count = _objectCount;

                // Add the objects that are contained in the children
                if (ChildTl != null)
                {
                    count += ChildTl.Count + ChildTr.Count + ChildBl.Count + ChildBr.Count;
                }

                return count;
            }
        }

        /// <summary>
        /// Count all nodes in the graph (Edge + Leaf)
        /// </summary>
        public int CountNodes
        {
            get
            {

                int count = _objectCount;

                // Add the objects that are contained in the children
                if (ChildTl != null)
                {
                    count += ChildTl.CountNodes + ChildTr.CountNodes + ChildBl.CountNodes + ChildBr.CountNodes + 4;
                }

                return count;
            }
        }

        /// <summary>
        /// Returns true if this is a empty leaf node
        /// </summary>
        public bool IsEmpty
        {
            get { return ChildTl == null && _objectCount == 0; }
        }

        public TNode ChildTl
        {
            get { return _childTl; }
        }

        public TNode ChildTr
        {
            get { return _childTr; }
        }

        public TNode ChildBl
        {
            get { return _childBl; }
        }

        public TNode ChildBr
        {
            get { return _childBr; }
        }

        public TNode Parent
        {
            get { return _parent; }
            internal set { _parent = value; }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a QuadTree for the specified area.
        /// </summary>
        /// <param name="rect">The area this QuadTree object will encompass.</param>
        protected QuadTreeFNodeCommon(RectangleF rect)
        {
            Rect = rect;
        }


        /// <summary>
        /// Creates a QuadTree for the specified area.
        /// </summary>
        /// <param name="x">The top-left position of the area rectangle.</param>
        /// <param name="y">The top-right position of the area rectangle.</param>
        /// <param name="width">The width of the area rectangle.</param>
        /// <param name="height">The height of the area rectangle.</param>
        protected QuadTreeFNodeCommon(float x, float y, float width, float height)
        {
            Rect = new RectangleF(x, y, width, height);
        }


        internal QuadTreeFNodeCommon(TNode parent, RectangleF rect)
            : this(rect)
        {
            _parent = parent;
        }

        #endregion

        #region Private Members

        /// <summary>
        /// Add an item to the object list.
        /// </summary>
        /// <param name="item">The item to add.</param>
        private void Add(QuadTreeObject<T, TNode> item)
        {
            if (_objects == null)
            {
                _objects = new QuadTreeObject<T, TNode>[MaxObjectsPerNode];
            }
            else if (_objectCount == _objects.Length)
            {
                var old = _objects;
                _objects = new QuadTreeObject<T, TNode>[old.Length*2];
                Array.Copy(old, _objects, old.Length);
            }
            Debug.Assert(_objectCount < _objects.Length);

            item.Owner = this as TNode;
            _objects[_objectCount ++] = item;
            Debug.Assert(_objects[_objectCount - 1] != null);
        }


        /// <summary>
        /// Remove an item from the object list.
        /// </summary>
        /// <param name="item">The object to remove.</param>
        internal bool Remove(QuadTreeObject<T, TNode> item)
        {
            if (_objects == null) return false;

            int removeIndex = Array.IndexOf(_objects, item, 0, _objectCount);
            if (removeIndex < 0) return false;

            if (_objectCount == 1)
            {
                _objects = null;
                _objectCount = 0;
            }
            else
            {
#if DEBUG
                item.Owner = null;
#endif
                _objects[removeIndex] = _objects[-- _objectCount];
                _objects[_objectCount] = null;
            }

            Debug.Assert(_objectCount >= 0);
            return true;
        }

        /// <summary>
        /// Automatically subdivide this QuadTree and move it's children into the appropriate Quads where applicable.
        /// </summary>
        internal PointF Subdivide(bool recursive = true)
        {
            float area = Rect.Width*Rect.Height;
            if (area < 0.01f || float.IsInfinity(area))
            {
                return new PointF(float.NaN, float.NaN);
            }

            // We've reached capacity, subdivide...
            PointF mid = new PointF(Rect.X + (Rect.Width/2), Rect.Y + (Rect.Height/2));

            Subdivide(mid, recursive);

            return mid;
        }


        /// <summary>
        /// Manually subdivide this QuadTree and move it's children into the appropriate Quads where applicable.
        /// </summary>
        public void Subdivide(PointF mid, bool recursive = true)
        {
            Debug.Assert(_childTl == null);
            // We've reached capacity, subdivide...
            _childTl = CreateNode(new RectangleF(Rect.Left, Rect.Top, mid.X - Rect.Left, mid.Y - Rect.Top));
            _childTr = CreateNode(new RectangleF(mid.X, Rect.Top, Rect.Right - mid.X, mid.Y - Rect.Top));
            _childBl = CreateNode(new RectangleF(Rect.Left, mid.Y, mid.X - Rect.Left, Rect.Bottom - mid.Y));
            _childBr = CreateNode(new RectangleF(mid.X, mid.Y, Rect.Right - mid.X, Rect.Bottom - mid.Y));
            Debug.Assert(GetChildren().All((a) => a.Parent == this));

            if (_objectCount != 0)
            {
                var nodeList = _objects.Take(_objectCount);
                _objects = null;
                _objectCount = 0;
                foreach (var a in nodeList) //todo: bulk insert optimization
                {
                    Insert(a, recursive);
                }
                Debug.Assert(Count == nodeList.Count());
            }
        }

        protected void VerifyNodeAssertions(RectangleF rectangleF)
        {
            Debug.Assert(rectangleF.Width > 0);
            Debug.Assert(rectangleF.Height > 0);
        }

        protected abstract TNode CreateNode(RectangleF rectangleF);

        public IEnumerable<TNode> GetChildren()
        {
            if (ChildTl == null)
            {
                Debug.Assert(null == ChildBl && null == ChildBr && null == ChildTr);
                yield break;
            }
            yield return ChildTl;
            yield return ChildTr;
            yield return ChildBl;
            yield return ChildBr;
        }

        /// <summary>
        /// Get the child Quad that would contain an object.
        /// </summary>
        /// <param name="item">The object to get a child for.</param>
        /// <returns></returns>
        private TNode GetDestinationTree(QuadTreeObject<T, TNode> item)
        {
            if (ChildTl == null)
            {
                return this as TNode;
            }

            if (ChildTl.ContainsObject(item))
            {
                return ChildTl;
            }
            if (ChildTr.ContainsObject(item))
            {
                return ChildTr;
            }
            if (ChildBl.ContainsObject(item))
            {
                return ChildBl;
            }
            if (ChildBr.ContainsObject(item))
            {
                return ChildBr;
            }

            // If a child can't contain an object, it will live in this Quad
            // This is usually when == midpoint
            return this as TNode;
        }

        internal void Relocate(QuadTreeObject<T, TNode> item)
        {
            // Are we still inside our parent?
            if (ContainsObject(item))
            {
                // Good, have we moved inside any of our children?
                if (ChildTl != null)
                {
                    TNode dest = GetDestinationTree(item);
                    if (item.Owner != dest)
                    {
                        // Delete the item from this quad and add it to our child
                        // Note: Do NOT clean during this call, it can potentially delete our destination quad
                        TNode formerOwner = item.Owner;
                        Delete(item, false);
                        dest.Insert(item);

                        // Clean up ourselves
                        formerOwner.CleanUpwards();
                    }
                }
            }
            else
            {
                // We don't fit here anymore, move up, if we can
                if (Parent != null)
                {
                    Parent.Relocate(item);
                }
            }
        }

        internal bool CleanThis()
        {
            if (ChildTl != null)
            {
                if (HasNoMoreThan(MaxObjectsPerNode))
                {
                    /* Has few nodes, your children are my children */
                    Dictionary<T, QuadTreeObject<T, TNode>> buffer = new Dictionary<T, QuadTreeObject<T, TNode>>(MaxObjectsPerNode);
                    GetAllObjects(a => buffer.Add(a.Data, a));

#if DEBUG
                    Dictionary<T, TNode> oldOwners = buffer.ToDictionary((a) => a.Key, (b) => b.Value.Owner);
#endif
                    foreach (var c in GetChildren())
                    {
                        c.Parent = null;
                    }
                    ClearRecursive();

                    AddBulk(buffer.Keys.ToArray(), (a) => buffer[a]);
#if DEBUG
                    Debug.Assert(_objects == null || _objects.All((a) => a == null || a.Owner != oldOwners[a.Data] || a.Owner == this));
#endif

                    return true;
                }

                var emptyChildren = GetChildren().Count((a) => a.IsEmpty);
                var beforeCount = Count;

                if (emptyChildren == 4)
                {
                    /* If all the children are empty leaves, delete all the children */
                    ClearChildren();
                }
                else if (emptyChildren == 3)
                {
                    /* Only one child has data, this child can be pushed up */
                    var child = GetChildren().First((a) => !a.IsEmpty);

                    //Move child's children up, we are now their parent
                    _childTl = child._childTl;
                    _childTr = child._childTr;
                    _childBl = child._childBl;
                    _childBr = child._childBr;
                    foreach (var c in GetChildren())
                    {
                        c.Parent = this as TNode;
                    }

                    //todo: expand these to fill, preserving middle point
                    if (_objectCount == 0)
                    {
                        _objects = child._objects;
                        _objectCount = child._objectCount;
                        for (int index = 0; index < _objectCount; index++)
                        {
                            _objects[index].Owner = this as TNode;
                        }
                    }
                    else
                    {
                        for (int index = 0; index < child._objectCount; index++)
                        {
                            Insert(child._objects[index]);
                        }
                        Debug.Assert(beforeCount + child._objectCount == Count);
                    }
                    if (child._objects != null)
                    {
                        Debug.Assert(child._objects.Take(child._objectCount).All(a => a.Owner != child));
                    }
                    child.Clear();
                    Debug.Assert(child.IsEmpty);
                }
                else if (emptyChildren != 0 && !HasAtleast(MaxOptimizeDeletionReAdd))
                {
                    /* If has an empty child & no more than OptimizeThreshold worth of data - rebuild more optimally */
                    Dictionary<T, QuadTreeObject<T, TNode>> buffer = new Dictionary<T, QuadTreeObject<T, TNode>>();
                    GetAllObjects((a) => buffer.Add(a.Data, a));

#if DEBUG
                    Dictionary<T, TNode> oldOwners = buffer.ToDictionary((a) => a.Key, (b) => b.Value.Owner);
#endif
                    foreach (var c in GetChildren())
                    {
                        c.Parent = null;
                    }
                    ClearRecursive();
                    AddBulk(buffer.Keys.ToArray(), (a) => buffer[a]);
#if DEBUG
                    Debug.Assert(_objects == null || _objects.All((a) => a == null || a.Owner != oldOwners[a.Data] || a.Owner == this));
#endif
                }
                else
                {
                    return false;
                }
                Debug.Assert(Count == beforeCount);
                Debug.Assert(_objects == null || _objects.All((a) => a == null || a.Owner == this));
            }
            return true;
        }

        private void ClearRecursive()
        {
            foreach (var child in GetChildren())
            {
                child.ClearRecursive();
            }
            Clear();
        }

        private UInt32 EncodeMorton2(UInt32 x, UInt32 y)
        {
            return (Part1By1(y) << 1) + Part1By1(x);
        }

        private UInt32 Part1By1(UInt32 x)
        {
            x &= 0x0000ffff; // x = ---- ---- ---- ---- fedc ba98 7654 3210
            x = (x ^ (x << 8)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
            x = (x ^ (x << 4)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
            x = (x ^ (x << 2)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
            x = (x ^ (x << 1)) & 0x55555555; // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
            return x;
        }

        private UInt32 MortonIndex2(PointF pointF, float minX, float minY, float width, float height)
        {
            pointF = new PointF(pointF.X - minX, pointF.Y - minY);
            var pX = (UInt32) (UInt16.MaxValue*pointF.X/width);
            var pY = (UInt32) (UInt16.MaxValue*pointF.Y/height);

            return EncodeMorton2(pX, pY);
        }

        protected abstract PointF GetMortonPoint(T p);

        internal void InsertStore(PointF tl, PointF br, T[] range, int start, int end,
            Func<T, QuadTreeObject<T, TNode>> createObject, int threadLevel, List<Task> tasks = null)
        {
            if (ChildTl != null)
            {
                throw new InvalidOperationException("Bulk insert can only be performed on a QuadTree without children");
            }

            var count = end - start;
            float area = (br.X - tl.X)*(br.Y - tl.Y);
            if (count > 8 && area > 0.01f && !float.IsInfinity(area))
            {
                //If we have more than 8 points and an area of 0.01 then we will subdivide

                //Calculate the offsets in the array for each quater
                var quater = count/4;
                var quater1 = start + quater + (count%4);
                var quater2 = quater1 + quater;
                var quater3 = quater2 + quater;
                Debug.Assert(quater3 + quater - start == count);

                IEnumerable<QuadTreeObject<T, TNode>> objects = null;
                if (_objectCount != 0)
                {
                    objects = _objects.Take(_objectCount);
                    _objects = null;
                    _objectCount = 0;
                }

                //The middlepoint is at the half way mark (2 quaters)
                PointF middlePoint = GetMortonPoint(range[quater2]);
                if (ContainsPoint(middlePoint) && tl.X != middlePoint.X && tl.Y != middlePoint.Y &&
                    br.X != middlePoint.X && br.Y != middlePoint.Y)
                {
                    Subdivide(middlePoint, false);
                }
                else
                {
                    middlePoint = Subdivide(false);
                    Debug.Assert(!float.IsNaN(middlePoint.X));
                }

                if (threadLevel == 0)
                {
                    ChildTl.InsertStore(tl, middlePoint, range, start, quater1, createObject, 0);
                    ChildTr.InsertStore(new PointF(middlePoint.X, tl.Y), new PointF(br.X, middlePoint.Y), range, quater1,
                        quater2, createObject, 0);
                    ChildBl.InsertStore(new PointF(tl.X, middlePoint.Y), new PointF(middlePoint.X, br.Y), range, quater2,
                        quater3, createObject, 0);
                    ChildBr.InsertStore(middlePoint, br, range, quater3, end, createObject, 0);
                }
                else
                {
                    Debug.Assert(objects == null || _objectCount == 0);
                    if (--threadLevel == 0)
                    {

                        tasks.Add(
                            Task.Run(
                                () => ChildTl.InsertStore(tl, middlePoint, range, start, quater1, createObject, 0)));
                        tasks.Add(
                            Task.Run(
                                () =>
                                    ChildTr.InsertStore(new PointF(middlePoint.X, tl.Y),
                                        new PointF(br.X, middlePoint.Y), range, quater1,
                                        quater2, createObject, 0)));
                        tasks.Add(
                            Task.Run(
                                () =>
                                    ChildBl.InsertStore(new PointF(tl.X, middlePoint.Y),
                                        new PointF(middlePoint.X, br.Y), range, quater2,
                                        quater3, createObject, 0)));
                        tasks.Add(
                            Task.Run(
                                () => ChildBr.InsertStore(middlePoint, br, range, quater3, end, createObject, 0)));

                        if (objects != null)
                        {
                            tasks.Add(new Task(() =>
                            {
                                foreach (var t in objects)
                                {
                                    Insert(t, false);
                                }
                            }));
                        }

                        return;
                    }
                    else
                    {
                        ChildTl.InsertStore(tl, middlePoint, range, start, quater1, createObject, threadLevel, tasks);
                        ChildTr.InsertStore(new PointF(middlePoint.X, tl.Y), new PointF(br.X, middlePoint.Y), range,
                            quater1,
                            quater2, createObject, threadLevel, tasks);
                        ChildBl.InsertStore(new PointF(tl.X, middlePoint.Y), new PointF(middlePoint.X, br.Y), range,
                            quater2,
                            quater3, createObject, threadLevel, tasks);
                        ChildBr.InsertStore(middlePoint, br, range, quater3, end, createObject, threadLevel, tasks);
                    }
                }

                if (objects != null)
                {
                    Debug.Assert(threadLevel == 0); //Only new QT's support threading
                    foreach (var t in objects)
                    {
                        Insert(t, false);
                    }
                }
            }
            else
            {
                for (; start < end; start++)
                {
                    var t = range[start];
                    var qto = createObject(t);
                    Insert(qto, false);
                }
            }
        }

        public void AddBulk(T[] points, Func<T, QuadTreeObject<T, TNode>> createObject, int threadLevel = 0)
        {
#if DEBUG
            if (ChildTl != null)
            {
                throw new InvalidOperationException("Bulk add can only be performed on a QuadTree without children");
            }
#endif

            if (points.Length + _objectCount <= MaxObjectsPerNode)
            {
                foreach (var p in points)
                {
                    Insert(createObject(p), false);
                }
                return;
            }

            //Find the max / min morton points
            int threads = 0;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            if (threadLevel > 0)
            {
                object lockObj = new object();
                threads = (int) Math.Pow(threadLevel, 4); //, (int)Math.Ceiling(((float)points.Length) / threads)
                if (points.Length > 0)
                {
                    Parallel.ForEach(Partitioner.Create(0, points.Length), (a) =>
                    {
                        float localMinX = float.MaxValue,
                            localMaxX = float.MinValue,
                            localMinY = float.MaxValue,
                            localMaxY = float.MinValue;
                        for (int i = a.Item1; i < a.Item2; i++)
                        {
                            var point = GetMortonPoint(points[i]);
                            if (point.X > localMaxX)
                            {
                                localMaxX = point.X;
                            }
                            if (point.X < localMinX)
                            {
                                localMinX = point.X;
                            }
                            if (point.Y > localMaxX)
                            {
                                localMaxX = point.Y;
                            }
                            if (point.Y < localMinY)
                            {
                                localMinY = point.Y;
                            }
                        }

                        lock (lockObj)
                        {
                            minX = Math.Min(localMinX, minX);
                            minY = Math.Min(localMinY, minY);
                            maxX = Math.Max(localMaxX, maxX);
                            maxY = Math.Max(localMaxY, maxY);
                        }
                    });
                }
            }
            else
            {
                foreach (var p in points)
                {
                    var point = GetMortonPoint(p);
                    if (point.X > maxX)
                    {
                        maxX = point.X;
                    }
                    if (point.X < minX)
                    {
                        minX = point.X;
                    }
                    if (point.Y > maxY)
                    {
                        maxY = point.Y;
                    }
                    if (point.Y < minY)
                    {
                        minY = point.Y;
                    }
                }
            }

            //Calculate the width and height of the morton space
            float width = maxX - minX, height = maxY - minY;

            //Return points sorted by motron point, MortonIndex2 is slow - so needs caching
            var range =
                points.Select(
                    (a) => new KeyValuePair<UInt32, T>(MortonIndex2(GetMortonPoint(a), minX, minY, width, height), a))
                    .OrderBy((a) => a.Key)
                    .Select((a) => a.Value)
                    .ToArray();
            Debug.Assert(range.Length == points.Count());

            List<Task> tasks = new List<Task>(threads);
            InsertStore(QuadRect.Location, new PointF(QuadRect.Bottom, QuadRect.Right), range, 0, range.Length,
                createObject, threadLevel, tasks);

            // 2 stage execution, first children - then add objects
            tasks.RemoveAll((task) =>
            {
                if (task.Status == TaskStatus.Created)
                {
                    task.Start();
                    return false;
                }
                task.Wait();
                return true;
            });
            foreach (var task in tasks)
            {
                task.Wait();
            }
        }

        internal void CleanUpwards()
        {
            if (CleanThis() && Parent != null)
            {
                Parent.CleanUpwards();
            }
        }

        #endregion

        public bool ContainsPoint(PointF point)
        {
            return Rect.Contains(point);
        }

        public abstract bool ContainsObject(QuadTreeObject<T, TNode> qto);

        #region Internal Methods

        private void ClearChildren()
        {
            foreach (var child in GetChildren())
            {
                child.Parent = null;
            }
            _childTl = _childTr = _childBl = _childBr = null;
        }

        /// <summary>
        /// Clears the QuadTree of all objects, including any objects living in its children.
        /// </summary>
        public void Clear()
        {
            // Clear out the children, if we have any
            if (ChildTl != null)
            {
                // Set the children to null
                ClearChildren();
            }

            // Clear any objects at this level
            if (_objects != null)
            {
                _objectCount = 0;
                _objects = null;
            }
            else
            {
                Debug.Assert(_objectCount == 0);
            }
        }

        private void _HasAtLeast(ref int objects)
        {
            objects -= _objectCount;
            if (objects > 0)
            {
                foreach (var child in GetChildren())
                {
                    child._HasAtLeast(ref objects);
                }
            }
        }

        public bool HasAtleast(int objects)
        {
            _HasAtLeast(ref objects);
            return objects <= 0;
        }

        public bool HasNoMoreThan(int objects)
        {
            return !HasAtleast(objects + 1);
        }


        /// <summary>
        /// Deletes an item from this QuadTree. If the object is removed causes this Quad to have no objects in its children, it's children will be removed as well.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <param name="clean">Whether or not to clean the tree</param>
        public void Delete(QuadTreeObject<T, TNode> item, bool clean)
        {
            if (item.Owner != null)
            {
                if (item.Owner == this)
                {
                    Remove(item);
                    if (clean)
                    {
                        CleanUpwards();
                    }
                }
                else
                {
                    item.Owner.Delete(item, clean);
                }
            }
        }


        /// <summary>
        /// Insert an item into this QuadTree object.
        /// </summary>
        /// <param name="item">The item to insert.</param>
        /// <param name="canSubdivide"></param>
        public void Insert(QuadTreeObject<T, TNode> item, bool canSubdivide = true)
        {
            // If this quad doesn't contain the items rectangle, do nothing, unless we are the root
            if (!CheckContains(Rect, item.Data))
            {
                Debug.Assert(Parent != null,
                    "We are not the root, and this object doesn't fit here. How did we get here?");
                if (Parent != null)
                {
                    // This object is outside of the QuadTree bounds, we should add it at the root level
                    Parent.Insert(item, canSubdivide);
                }
                return;
            }

            if (_objects == null ||
                (ChildTl == null && _objectCount + 1 <= MaxObjectsPerNode))
            {
                // If there's room to add the object, just add it
                Add(item);
            }
            else
            {
                // No quads, create them and bump objects down where appropriate
                if (ChildTl == null)
                {
                    if (canSubdivide)
                    {
                        Subdivide();
                    }
                    else
                    {
                        Add(item);
                        return;
                    }
                }

                // Find out which tree this object should go in and add it there
                TNode destTree = GetDestinationTree(item);
                if (destTree == this)
                {
                    Add(item);
                }
                else
                {
                    destTree.Insert(item, canSubdivide);
                }
            }
        }

        protected abstract bool CheckContains(RectangleF rectangleF, T data);


        /// <summary>
        /// Get the objects in this tree that intersect with the specified rectangle.
        /// </summary>
        /// <param name="searchRect">The RectangleF to find objects in.</param>
        public List<T> GetObjects(TQuery searchRect)
        {
            var results = new List<T>();
            GetObjects(searchRect, (ref T obj) => results.Add(obj));
            return results;
        }

        protected abstract bool QueryContains(TQuery search, RectangleF rect);
        protected abstract bool QueryIntersects(TQuery search, RectangleF rect);

        /// <summary>
        /// Get the objects in this tree that intersect with the specified rectangle.
        /// </summary>
        /// <param name="searchRect">The RectangleF to find objects in.</param>
        public IEnumerable<T> EnumObjects(TQuery searchRect)
        {
            Stack<TNode> stack = new Stack<TNode>();
            Stack<TNode> allStack = null;
            TNode node = this as TNode;
            do
            {
                if (QueryContains(searchRect, node.Rect))
                {
                    // If the search area completely contains this quad, just get every object this quad and all it's children have
                    allStack = allStack ?? new Stack<TNode>();
                    do
                    {
                        if (node._objects != null)
                        {
                            for (int i = 0; i < node._objectCount; i++)
                            {
                                var y = node._objects[i];
                                yield return y.Data;
                            }
                        }
                        if (node.ChildTl != null)
                        {
                            allStack.Push(node.ChildTl);
                            allStack.Push(node.ChildTr);
                            allStack.Push(node.ChildBl);
                            allStack.Push(node.ChildBr);
                        }
                        if (allStack.Count == 0)
                        {
                            break;
                        }
                        node = allStack.Pop();
                    } while (true);
                }
                else if (QueryIntersects(searchRect, node.Rect))
                {
                    // Otherwise, if the quad isn't fully contained, only add objects that intersect with the search rectangle
                    if (node._objects != null)
                    {
                        for (int i = 0; i < node._objectCount; i++)
                        {
                            QuadTreeObject<T, TNode> t = node._objects[i];
                            if (CheckIntersects(searchRect, t.Data))
                            {
                                yield return t.Data;
                            }
                        }
                    }

                    // Get the objects for the search RectangleF from the children
                    if (node.ChildTl != null)
                    {
                        stack.Push(node.ChildTl);
                        stack.Push(node.ChildTr);
                        stack.Push(node.ChildBl);
                        stack.Push(node.ChildBr);
                    }
                }
                if (stack.Count == 0)
                {
                    break;
                }
                node = stack.Pop();
            } while (true);
        }

        protected abstract bool CheckIntersects(TQuery searchRect, T data);

        /// <summary>
        /// Get the objects in this tree that intersect with the specified rectangle.
        /// </summary>
        /// <param name="searchRect">The RectangleF to find objects in.</param>
        /// <param name="put"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ObjectCount(TQuery searchRect) {
            
            var count = 0;
            
            // We can't do anything if the results list doesn't exist
            if (QueryContains(searchRect, Rect))
            {
                // If the search area completely contains this quad, just get every object this quad and all it's children have
                count += AllObjectsCount();
            }
            else if (QueryIntersects(searchRect, Rect))
            {
                // Otherwise, if the quad isn't fully contained, only add objects that intersect with the search rectangle
                if (_objects != null)
                {
                    for (int i = 0; i < _objectCount; i++)
                    {
                        var data = _objects[i].Data;
                        if (CheckIntersects(searchRect, data)) {
                            count++;
                        }
                    }
                }

                // Get the objects for the search RectangleF from the children
                if (ChildTl != null)
                {
                    Debug.Assert(ChildTl != this);
                    Debug.Assert(ChildTr != this);
                    Debug.Assert(ChildBl != this);
                    Debug.Assert(ChildBr != this);
                    count += ChildTl.ObjectCount(searchRect);
                    count += ChildTr.ObjectCount(searchRect);
                    count += ChildBl.ObjectCount(searchRect);
                    count += ChildBr.ObjectCount(searchRect);
                }
                else
                {
                    Debug.Assert(ChildTr == null);
                    Debug.Assert(ChildBl == null);
                    Debug.Assert(ChildBr == null);
                }
            }

            return count;
        }

        /// <summary>
        /// Counts all objects in the node and returns their size 
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AllObjectsCount() {

            var count = 0;
            
            // If this Quad has objects, add them
            if (_objects != null)
            {
                Debug.Assert(_objectCount != 0);
                Debug.Assert(_objectCount == _objects.Count((a) => a != null));

                for (int i = 0; i < _objectCount; i++)
                {
                    if (_objects[i].Owner != this) break; //todo: better?
                    count++;
                }
            }
            else
            {
                Debug.Assert(_objectCount == 0);
            }

            // If we have children, get their objects too
            if (ChildTl != null)
            {
                count += ChildTl.AllObjectsCount();
                count += ChildTr.AllObjectsCount();
                count += ChildBl.AllObjectsCount();
                count += ChildBr.AllObjectsCount();
            }

            return count;
        }
        
                /// <summary>
        /// Get the objects in this tree that intersect with the specified rectangle.
        /// </summary>
        /// <param name="searchRect">The RectangleF to find objects in.</param>
        /// <param name="put"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetObjects(TQuery searchRect, ForObject<T> put)
        {
            // We can't do anything if the results list doesn't exist
            if (QueryContains(searchRect, Rect))
            {
                // If the search area completely contains this quad, just get every object this quad and all it's children have
                GetAllObjects(put);
            }
            else if (QueryIntersects(searchRect, Rect))
            {
                // Otherwise, if the quad isn't fully contained, only add objects that intersect with the search rectangle
                if (_objects != null)
                {
                    for (int i = 0; i < _objectCount; i++)
                    {
                        var data = _objects[i].Data;
                        if (CheckIntersects(searchRect, data))
                        {
                            put(data);
                        }
                    }
                }

                // Get the objects for the search RectangleF from the children
                if (ChildTl != null)
                {
                    Debug.Assert(ChildTl != this);
                    Debug.Assert(ChildTr != this);
                    Debug.Assert(ChildBl != this);
                    Debug.Assert(ChildBr != this);
                    ChildTl.GetObjects(searchRect, put);
                    ChildTr.GetObjects(searchRect, put);
                    ChildBl.GetObjects(searchRect, put);
                    ChildBr.GetObjects(searchRect, put);
                }
                else
                {
                    Debug.Assert(ChildTr == null);
                    Debug.Assert(ChildBl == null);
                    Debug.Assert(ChildBr == null);
                }
            }
        }
        
        /// <summary>
        /// Get all objects in this Quad, and it's children.
        /// </summary>
        /// <param name="put">A reference to a list in which to store the objects.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetAllObjects(ForObject<T> put)
        {
            GetAllObjects((o) => put(o._data));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetAllObjects(ForObject<QuadTreeObject<T, TNode>> put)
        {
            // If this Quad has objects, add them
            if (_objects != null)
            {
                Debug.Assert(_objectCount != 0);
                Debug.Assert(_objectCount == _objects.Count((a) => a != null));

                for (int i = 0; i < _objectCount; i++)
                {
                    if (_objects[i].Owner != this) break; //todo: better?
                    put(_objects[i]);
                }
            }
            else
            {
                Debug.Assert(_objectCount == 0);
            }

            // If we have children, get their objects too
            if (ChildTl != null)
            {
                ChildTl.GetAllObjects(put);
                ChildTr.GetAllObjects(put);
                ChildBl.GetAllObjects(put);
                ChildBr.GetAllObjects(put);
            }
        }
        
                /// <summary>
        /// Get the objects in this tree that intersect with the specified rectangle.
        /// </summary>
        /// <param name="searchRect">The RectangleF to find objects in.</param>
        /// <param name="put"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetObjects<P>(TQuery searchRect, ref P payload, ForObject<P,T> put)
        {
            // We can't do anything if the results list doesn't exist
            if (QueryContains(searchRect, Rect))
            {
                // If the search area completely contains this quad, just get every object this quad and all it's children have
                GetAllObjects(ref payload, put);
            }
            else if (QueryIntersects(searchRect, Rect))
            {
                // Otherwise, if the quad isn't fully contained, only add objects that intersect with the search rectangle
                if (_objects != null)
                {
                    for (int i = 0; i < _objectCount; i++)
                    {
                        var data = _objects[i].Data;
                        if (CheckIntersects(searchRect, data))
                        {
                            put(ref payload, data);
                        }
                    }
                }

                // Get the objects for the search RectangleF from the children
                if (ChildTl != null)
                {
                    Debug.Assert(ChildTl != this);
                    Debug.Assert(ChildTr != this);
                    Debug.Assert(ChildBl != this);
                    Debug.Assert(ChildBr != this);
                    ChildTl.GetObjects(searchRect, ref payload, put);
                    ChildTr.GetObjects(searchRect, ref payload, put);
                    ChildBl.GetObjects(searchRect, ref payload, put);
                    ChildBr.GetObjects(searchRect, ref payload, put);
                }
                else
                {
                    Debug.Assert(ChildTr == null);
                    Debug.Assert(ChildBl == null);
                    Debug.Assert(ChildBr == null);
                }
            }
        }
        
        /// <summary>
        /// Get all objects in this Quad, and it's children.
        /// </summary>
        /// <param name="put">A reference to a list in which to store the objects.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetAllObjects<P>(ref P payload, ForObject<P,T> put)
        {
            GetAllObjects(ref payload, (ref P load, QuadTreeObject<T, TNode> o) => put(ref load, o._data));
        }
        
        /// <summary>
        /// Get the objects in this tree that intersect with the specified rectangle.
        /// </summary>
        /// <param name="searchRect">The RectangleF to find objects in.</param>
        /// <param name="put"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetObjects(TQuery searchRect, ForObjectStruct<T> put)
        {
            // We can't do anything if the results list doesn't exist
            if (QueryContains(searchRect, Rect))
            {
                // If the search area completely contains this quad, just get every object this quad and all it's children have
                GetAllObjects(put);
            }
            else if (QueryIntersects(searchRect, Rect))
            {
                // Otherwise, if the quad isn't fully contained, only add objects that intersect with the search rectangle
                if (_objects != null)
                {
                    for (int i = 0; i < _objectCount; i++)
                    {
                        var data = _objects[i].Data;
                        if (CheckIntersects(searchRect, data))
                        {
                            put(ref data);
                        }
                    }
                }

                // Get the objects for the search RectangleF from the children
                if (ChildTl != null)
                {
                    Debug.Assert(ChildTl != this);
                    Debug.Assert(ChildTr != this);
                    Debug.Assert(ChildBl != this);
                    Debug.Assert(ChildBr != this);
                    ChildTl.GetObjects(searchRect, put);
                    ChildTr.GetObjects(searchRect, put);
                    ChildBl.GetObjects(searchRect, put);
                    ChildBr.GetObjects(searchRect, put);
                }
                else
                {
                    Debug.Assert(ChildTr == null);
                    Debug.Assert(ChildBl == null);
                    Debug.Assert(ChildBr == null);
                }
            }
        }
        
        /// <summary>
        /// Get all objects in this Quad, and it's children.
        /// </summary>
        /// <param name="put">A reference to a list in which to store the objects.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetAllObjects(ForObjectStruct<T> put)
        {
            GetAllObjects((o) => put(ref o._data));
        }
        
        /// <summary>
        /// Get the objects in this tree that intersect with the specified rectangle.
        /// </summary>
        /// <param name="searchRect">The RectangleF to find objects in.</param>
        /// <param name="put"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetObjects<P>(TQuery searchRect, ref P payload, ForObjectStruct<P,T> put)
        {
            // We can't do anything if the results list doesn't exist
            if (QueryContains(searchRect, Rect))
            {
                // If the search area completely contains this quad, just get every object this quad and all it's children have
                GetAllObjects(ref payload, put);
            }
            else if (QueryIntersects(searchRect, Rect))
            {
                // Otherwise, if the quad isn't fully contained, only add objects that intersect with the search rectangle
                if (_objects != null)
                {
                    for (int i = 0; i < _objectCount; i++)
                    {
                        var data = _objects[i].Data;
                        if (CheckIntersects(searchRect, data))
                        {
                            put(ref payload, ref data);
                        }
                    }
                }

                // Get the objects for the search RectangleF from the children
                if (ChildTl != null)
                {
                    Debug.Assert(ChildTl != this);
                    Debug.Assert(ChildTr != this);
                    Debug.Assert(ChildBl != this);
                    Debug.Assert(ChildBr != this);
                    ChildTl.GetObjects(searchRect, ref payload, put);
                    ChildTr.GetObjects(searchRect, ref payload, put);
                    ChildBl.GetObjects(searchRect, ref payload, put);
                    ChildBr.GetObjects(searchRect, ref payload, put);
                }
                else
                {
                    Debug.Assert(ChildTr == null);
                    Debug.Assert(ChildBl == null);
                    Debug.Assert(ChildBr == null);
                }
            }
        }
        
        /// <summary>
        /// Get all objects in this Quad, and it's children.
        /// </summary>
        /// <param name="put">A reference to a list in which to store the objects.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetAllObjects<P>(ref P payload, ForObjectStruct<P,T> put)
        {
            GetAllObjects(ref payload, (ref P load, QuadTreeObject<T, TNode> o) => put(ref load, ref o._data));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetAllObjects<P>(ref P payload, ForObject<P, QuadTreeObject<T, TNode>> put)
        {
            // If this Quad has objects, add them
            if (_objects != null)
            {
                Debug.Assert(_objectCount != 0);
                Debug.Assert(_objectCount == _objects.Count((a) => a != null));

                for (int i = 0; i < _objectCount; i++)
                {
                    if (_objects[i].Owner != this) break; //todo: better?
                    put(ref payload, _objects[i]);
                }
            }
            else
            {
                Debug.Assert(_objectCount == 0);
            }

            // If we have children, get their objects too
            if (ChildTl != null)
            {
                ChildTl.GetAllObjects(ref payload, put);
                ChildTr.GetAllObjects(ref payload, put);
                ChildBl.GetAllObjects(ref payload, put);
                ChildBr.GetAllObjects(ref payload, put);
            }
        }

        
        /// <summary>
        /// Get the objects in this tree that intersect with the specified rectangle.
        /// </summary>
        /// <param name="searchRect">The RectangleF to find objects in.</param>
        /// <param name="put"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetObjects(TQuery searchRect, Span<T> array, int count = 0)
        {
            // We can't do anything if the results list doesn't exist
            if (QueryContains(searchRect, Rect))
            {
                // If the search area completely contains this quad, just get every object this quad and all it's children have
                count = GetAllObjects(array, count);
            }
            else if (QueryIntersects(searchRect, Rect))
            {
                // Otherwise, if the quad isn't fully contained, only add objects that intersect with the search rectangle
                if (_objects != null)
                {
                    for (int i = 0; i < _objectCount; i++)
                    {
                        var data = _objects[i].Data;
                        if (CheckIntersects(searchRect, data)) {
                            array[count] = data;
                            count++;
                        }
                    }
                }

                // Get the objects for the search RectangleF from the children
                if (ChildTl != null)
                {
                    Debug.Assert(ChildTl != this);
                    Debug.Assert(ChildTr != this);
                    Debug.Assert(ChildBl != this);
                    Debug.Assert(ChildBr != this);
                    count = ChildTl.GetObjects(searchRect, array, count);
                    count = ChildTr.GetObjects(searchRect, array, count);
                    count = ChildBl.GetObjects(searchRect, array, count);
                    count = ChildBr.GetObjects(searchRect, array, count);
                }
                else
                {
                    Debug.Assert(ChildTr == null);
                    Debug.Assert(ChildBl == null);
                    Debug.Assert(ChildBr == null);
                }
            }

            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetAllObjects(Span<T> array, int count = 0)
        {
            // If this Quad has objects, add them
            if (_objects != null)
            {
                Debug.Assert(_objectCount != 0);
                Debug.Assert(_objectCount == _objects.Count((a) => a != null));

                for (int i = 0; i < _objectCount; i++)
                {
                    if (_objects[i].Owner != this) break; //todo: better?
                    array[count] = _objects[i].Data;
                    count++;
                }
            }
            else
            {
                Debug.Assert(_objectCount == 0);
            }

            // If we have children, get their objects too
            if (ChildTl != null)
            {
                count = ChildTl.GetAllObjects(array, count);
                count = ChildTr.GetAllObjects(array, count);
                count = ChildBl.GetAllObjects(array, count);
                count = ChildBr.GetAllObjects(array, count);
            }

            return count;
        }

        private PointF CalculateBalance(out float top, out float bottom, out float left, out float right)
        {
            var counts = new List<float>(4);
            int i = 0;
            foreach (var c in GetChildren())
            {
                counts[i++] = c.Count;
            }

            if (counts.Sum() < MinBalance)
            {
                top = 0;
                bottom = 0;
                left = 0;
                right = 0;
                return new PointF(1, 1);
            }

            top = counts[0] + counts[1];
            bottom = counts[2] + counts[3];
            float yBalance = top/bottom;
            left = counts[0] + counts[3];
            right = counts[1] + counts[4];
            float xBalance = left/right;

            return new PointF(xBalance, yBalance);
        }
    }


    public abstract class QuadTreeFNodeCommon<T, TNode> : QuadTreeFNodeCommon<T, TNode, RectangleF>
        where TNode : QuadTreeFNodeCommon<T, TNode>
    {
        protected QuadTreeFNodeCommon(RectangleF rect) : base(rect)
        {
        }

        protected QuadTreeFNodeCommon(float x, float y, float width, float height)
            : base(x, y, width, height)
        {
        }

        public QuadTreeFNodeCommon(TNode parent, RectangleF rect) : base(parent, rect)
        {
        }


        protected override bool QueryContains(RectangleF search, RectangleF rect)
        {
            return search.Contains(rect);
        }

        protected override bool QueryIntersects(RectangleF search, RectangleF rect)
        {
            return search.Intersects(rect);
        }

        /*
        public void Rebalance()
        {
            if (ChildTl == null)
            {
                return;
            }

            var centerPoint = CenterPoint;
            float top, bottom, left, right;
            var balance = CalculateBalance(out top, out bottom, out left, out right);
            var yB = Math.Abs(balance.Y - 1);
            var xB = Math.Abs(balance.X - 1);
            var xVy = yB - xB;
            float incrementer;
            bool inv = false;

            if (xVy > 0)
            {
                incrementer = QuadRect.Height * yB * 0.3f;
                if (balance.Y < (1 + ReBalanceOffset))
                {
                    //Top Heavy
                    inv = true;
                }
                else if (balance.Y < (1 - ReBalanceOffset))
                {
                    //Bottom Heavy
                }
                else
                {
                    return;
                }

                RectangleF searchRect;
                
                List<T> buffer = new List<T>();
                List<T> bufferCopy = new List<T>();
                float newB;
                do
                {
                    if (inv)
                    {
                        searchRect = new RectangleF(QuadRect.X, QuadRect.Y - incrementer, QuadRect.Width, incrementer);
                    }
                    else
                    {
                        searchRect = new RectangleF(QuadRect.X, QuadRect.Y, QuadRect.Width, incrementer);
                    }

                    GetObjects(searchRect, buffer.Add);

                    newB =
                        Math.Abs((top +
                                  (inv ? buffer.Count : -buffer.Count)/(bottom + (inv ? -buffer.Count : buffer.Count))) -
                                 1);

                    if (newB <= yB)
                    {
                        incrementer *= 2;
                        bufferCopy = buffer;
                        buffer = new List<T>(buffer.Capacity);
                    }
                } while (newB <= yB);

                if (bufferCopy.Any())
                {
                    if (inv)
                    {
                        
                    }
                }
            }
            else
            {

                if (balance.X < (1 + ReBalanceOffset))
                {
                    //Left Heavy
                }
                else if (balance.X < (1 - ReBalanceOffset))
                {
                    //Right Heavy
                }
                else
                {
                    return;
                }
            }
        }*/

        #endregion
    }

    public abstract class QuadTreeNodeFCommonPoint<T, TNode> : QuadTreeFNodeCommon<T, TNode, PointF>
        where TNode : QuadTreeFNodeCommon<T, TNode, PointF>
    {
        protected QuadTreeNodeFCommonPoint(RectangleF rect)
            : base(rect)
        {
        }

        protected QuadTreeNodeFCommonPoint(int x, int y, int width, int height)
            : base(x, y, width, height)
        {
        }

        public QuadTreeNodeFCommonPoint(TNode parent, RectangleF rect)
            : base(parent, rect)
        {
        }


        protected override bool QueryContains(PointF search, RectangleF rect)
        {
            return rect.Contains(search);
        }

        protected override bool QueryIntersects(PointF search, RectangleF rect)
        {
            return false;
        }
    }
}
