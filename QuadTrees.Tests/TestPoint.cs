﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using QuadTrees.QTreePointF;
using QuadTrees.QTreeRectF;

namespace QuadTrees.Tests
{
    [TestFixture]
    public class TestPoint
    {
        class QTreeObject: IPointFQuadStorable
        {
            private PointF _rect;

            public PointF Point {
                set { _rect = value; }
                get { return _rect; }
            }

            public QTreeObject(PointF rect)
            {
                _rect = rect;
            }
        }
        
        struct QTreeObjectStruct: IPointFQuadStorable {

            public int uniqueId;
            private PointF _rect;

            public PointF Point
            {
                set { _rect = value; }
                get { return _rect; }
            }

            public QTreeObjectStruct(int uniqueId, PointF rect) {
                this.uniqueId = uniqueId;
                _rect = rect;
            }

            public bool Equals(QTreeObjectStruct other) {
                return uniqueId == other.uniqueId;
            }

            public override bool Equals(object obj) {
                return obj is QTreeObjectStruct other && Equals(other);
            }

            public override int GetHashCode() {
                return uniqueId;
            }

        }
        
        public struct Payload {
            public int count;
        };
        
        [TestCase]
        public void TestCountQuery()
        {
            QuadTreePointF<QTreeObject> qtree = new QuadTreePointF<QTreeObject>(new RectangleF(float.MinValue/2,float.MinValue/2,float.MaxValue,float.MaxValue));
            qtree.AddRange(new List<QTreeObject>
            {
                new QTreeObject(new PointF(10,10)),
                new QTreeObject(new PointF(11,11)),
                new QTreeObject(new PointF(12,12)),
                new QTreeObject(new PointF(11,11)),
                new QTreeObject(new PointF(-1000,1000))
            });

            var list = qtree.GetObjects(new RectangleF(9, 9, 20, 20));
            var count = qtree.ObjectCount(new RectangleF(9, 9, 20, 20));

            Assert.AreEqual(4, list.Count);
            Assert.AreEqual(4, count);
        }
        
        [TestCase]
        public void TestForObject()
        {
            QuadTreePointF<QTreeObject> qtree = new QuadTreePointF<QTreeObject>(new RectangleF(float.MinValue/2,float.MinValue/2,float.MaxValue,float.MaxValue));
            qtree.AddRange(new List<QTreeObject>
            {
                new QTreeObject(new PointF(10,10)),
                new QTreeObject(new PointF(11,11)),
                new QTreeObject(new PointF(12,12)),
                new QTreeObject(new PointF(11,11)),
                new QTreeObject(new PointF(-1000,1000))
            });

            var count = 0;
            qtree.GetObjects(new RectangleF(9, 9, 20, 20), (ref QTreeObject o) => {
                count++;
            });
            Assert.AreEqual(4, count);
        }
        
        [TestCase]
        public void TestForObjectPayload()
        {

            QuadTreePointF<QTreeObject> qtree = new QuadTreePointF<QTreeObject>(new RectangleF(float.MinValue/2,float.MinValue/2,float.MaxValue,float.MaxValue));
            qtree.AddRange(new List<QTreeObject>
            {
                new QTreeObject(new PointF(10,10)),
                new QTreeObject(new PointF(11,11)),
                new QTreeObject(new PointF(12,12)),
                new QTreeObject(new PointF(11,11)),
                new QTreeObject(new PointF(-1000,1000))
            });

            var payload = new Payload{ count = 0 };
            qtree.GetObjects(new RectangleF(9, 9, 20, 20), ref payload, (ref Payload p, ref QTreeObject o) => {
                p.count++;
            });
            Assert.AreEqual(4, payload.count);
        }

        [TestCase]
        public void TestListQuery()
        {
            QuadTreePointF<QTreeObject> qtree = new QuadTreePointF<QTreeObject>(new RectangleF(float.MinValue/2,float.MinValue/2,float.MaxValue,float.MaxValue));
            qtree.AddRange(new List<QTreeObject>
            {
                new QTreeObject(new PointF(10,10)),
                new QTreeObject(new PointF(-1000,1000))
            });

            var list = qtree.GetObjects(new RectangleF(9, 9, 20, 20));
            var count = qtree.ObjectCount(new RectangleF(9, 9, 20, 20));
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(1, count);
        }
        
        [TestCase]
        public void TestListQueryOutput()
        {
            QuadTreePointF<QTreeObject> qtree = new QuadTreePointF<QTreeObject>(new RectangleF(float.MinValue/2,float.MinValue/2,float.MaxValue,float.MaxValue));
            qtree.AddRange(new List<QTreeObject>
            {
                new QTreeObject(new PointF(10,10)),
                new QTreeObject(new PointF(-1000,1000))
            }); ;

            var list = new List<QTreeObject>();
            qtree.GetObjects(new RectangleF(9, 9, 20, 20), list);
            Assert.AreEqual(1, list.Count);
        }
        [TestCase]
        public void TestListQueryEnum()
        {
            QuadTreePointF<QTreeObject> qtree = new QuadTreePointF<QTreeObject>(new RectangleF(float.MinValue/2,float.MinValue/2,float.MaxValue,float.MaxValue));
            qtree.AddRange(new List<QTreeObject>
            {
                new QTreeObject(new PointF(10,10)),
                new QTreeObject(new PointF(-1000,1000))
            });

            var list = qtree.EnumObjects(new RectangleF(9, 9, 20, 20));
            Assert.AreEqual(1, list.Count());
        }
        [TestCase]
        public void TestListGetAll()
        {
            QuadTreePointF<QTreeObject> qtree = new QuadTreePointF<QTreeObject>(new RectangleF(float.MinValue/2,float.MinValue/2,float.MaxValue,float.MaxValue));
            qtree.AddRange(new List<QTreeObject>
            {
                new QTreeObject(new PointF(10,10)),
                new QTreeObject(new PointF(-1000,1000))
            });

            var list = qtree.GetAllObjects();
            Assert.AreEqual(2, list.Count());
        }

        [TestCase]
        public void TestGetObjectsArray() {

            var list = new List<QTreeObject> {
                new QTreeObject(new PointF(10, 10)),
                new QTreeObject(new PointF(11, 11)),
                new QTreeObject(new PointF(100, 10)),
                new QTreeObject(new PointF(12, 12)),
                new QTreeObject(new PointF(11, 11)),
                new QTreeObject(new PointF(-1000, 1000))
            };
            
            QuadTreePointF<QTreeObject> qtree = new QuadTreePointF<QTreeObject>(new RectangleF(float.MinValue/2,float.MinValue/2,float.MaxValue,float.MaxValue));
            qtree.AddRange(list);

            var rect = new RectangleF(9, 9, 20, 20);
            var count = qtree.ObjectCount(rect);
            var array = new QTreeObject[count];
            qtree.GetObjects(rect, array);
            
            Assert.AreEqual(4, count);
            Assert.AreEqual(array[0], list[0]);
            Assert.AreEqual(array[1], list[1]);
            Assert.AreEqual(array[2], list[3]);
            Assert.AreEqual(array[3], list[4]);
        }
        
        [TestCase]
        public void TestGetObjectsArrayStack() {

            var list = new List<QTreeObjectStruct> {
                new QTreeObjectStruct(1,new PointF(10, 10)),
                new QTreeObjectStruct(2,new PointF(11, 11)),
                new QTreeObjectStruct(3,new PointF(100, 10)),
                new QTreeObjectStruct(4,new PointF(12, 12)),
                new QTreeObjectStruct(5,new PointF(13, 13)),
                new QTreeObjectStruct(6,new PointF(-1000, 1000))
            };
            
            QuadTreePointF<QTreeObjectStruct> qtree = new QuadTreePointF<QTreeObjectStruct>(new RectangleF(float.MinValue/2,float.MinValue/2,float.MaxValue,float.MaxValue));
            qtree.AddRange(list);

            var rect = new RectangleF(9, 9, 20, 20);
            var count = qtree.ObjectCount(rect);
            Span<QTreeObjectStruct> array = stackalloc QTreeObjectStruct[count];
            qtree.GetObjects(rect, array);
            
            Assert.AreEqual(4, count);
            Assert.AreEqual(array[0], list[0]);
            Assert.AreEqual(array[1], list[1]);
            Assert.AreEqual(array[2], list[3]);
            Assert.AreEqual(array[3], list[4]);
        }
        
        [TestCase]
        public void TestMove() {
            
            var obj = new QTreeObject(new PointF(5, 5));
            QuadTreePointF<QTreeObject> qtree = new QuadTreePointF<QTreeObject>(new RectangleF(float.MinValue/2,float.MinValue/2,float.MaxValue,float.MaxValue));
            qtree.AddRange(new List<QTreeObject>
            {
                obj,
                new QTreeObject(new PointF(-1000,1000))
            });

            obj.Point = new PointF(11, 11);
            qtree.Move(obj);

            var list = qtree.GetObjects(new RectangleF(10, 10, 20, 20));
            Assert.AreEqual(1, list.Count);
        }
        
        [TestCase]
        public void TestMoveStruct() {
            
            var obj = new QTreeObjectStruct(1,new PointF(5, 5));
            QuadTreePointF<QTreeObjectStruct> qtree = new QuadTreePointF<QTreeObjectStruct>(new RectangleF(float.MinValue/2,float.MinValue/2,float.MaxValue,float.MaxValue));
            qtree.AddRange(new List<QTreeObjectStruct>
            {
                obj,
                new QTreeObjectStruct(2,new PointF(-1000,1000))
            });

            obj.Point = new PointF(11, 11);
            qtree.Move(obj);

            var list = qtree.GetObjects(new RectangleF(10, 10, 20, 20));
            Assert.AreEqual(1, list.Count);
        }
    }
}
