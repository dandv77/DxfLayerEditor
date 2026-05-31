using System;
using System.Collections.Generic;
using System.Linq;
using DxfLayerEditor.Algorithms;
using DxfLayerEditor.Models;
using Xunit;

namespace DxfLayerEditor.Tests
{
    /// <summary>
    /// Unit tests for Union-Find data structure and chain detection algorithm.
    /// </summary>
    public class ChainDetectionTests
    {
        #region Union-Find

        [Fact]
        public void UnionFind_SeparateElements_NotConnected()
        {
            var uf = new UnionFind();
            uf.Find("a");
            uf.Find("b");

            Assert.False(uf.Connected("a", "b"));
        }

        [Fact]
        public void UnionFind_AfterUnion_Connected()
        {
            var uf = new UnionFind();
            uf.Union("a", "b");

            Assert.True(uf.Connected("a", "b"));
        }

        [Fact]
        public void UnionFind_TransitiveUnion_AllConnected()
        {
            var uf = new UnionFind();
            uf.Union("a", "b");
            uf.Union("b", "c");

            Assert.True(uf.Connected("a", "c"));
        }

        [Fact]
        public void UnionFind_GetGroups_CorrectClustering()
        {
            var uf = new UnionFind();
            uf.Union("a", "b");
            uf.Union("c", "d");
            uf.Find("e"); // standalone

            var groups = uf.GetGroups();

            Assert.Equal(3, groups.Count);
        }

        #endregion

        #region Chain Detection

        [Fact]
        public void DetectChains_ConnectedLines_SameChain()
        {
            // Two lines sharing an endpoint
            var line1 = new LineEntity(new Point2D(0, 0), new Point2D(1, 0));
            var line2 = new LineEntity(new Point2D(1, 0), new Point2D(2, 0));

            var entities = new List<EntityBase> { line1, line2 };
            var chains = ChainDetection.DetectChains(entities);

            Assert.Single(chains);
            Assert.Equal(2, chains[0].EntityIds.Count);
            Assert.Equal(line1.ChainId, line2.ChainId);
        }

        [Fact]
        public void DetectChains_DisconnectedLines_DifferentChains()
        {
            var line1 = new LineEntity(new Point2D(0, 0), new Point2D(1, 0));
            var line2 = new LineEntity(new Point2D(5, 5), new Point2D(6, 5));

            var entities = new List<EntityBase> { line1, line2 };
            var chains = ChainDetection.DetectChains(entities);

            Assert.Equal(2, chains.Count);
            Assert.NotEqual(line1.ChainId, line2.ChainId);
        }

        [Fact]
        public void DetectChains_ClosedTriangle_SingleChain()
        {
            var line1 = new LineEntity(new Point2D(0, 0), new Point2D(1, 0));
            var line2 = new LineEntity(new Point2D(1, 0), new Point2D(0.5, 1));
            var line3 = new LineEntity(new Point2D(0.5, 1), new Point2D(0, 0));

            var entities = new List<EntityBase> { line1, line2, line3 };
            var chains = ChainDetection.DetectChains(entities);

            Assert.Single(chains);
            Assert.Equal(3, chains[0].EntityIds.Count);
        }

        [Fact]
        public void DetectChains_NearbyEndpoints_WithinTolerance_Grouped()
        {
            // Endpoints differ by less than tolerance
            var line1 = new LineEntity(new Point2D(0, 0), new Point2D(1.0, 0));
            var line2 = new LineEntity(new Point2D(1.00005, 0.00003), new Point2D(2, 0));

            var entities = new List<EntityBase> { line1, line2 };
            var chains = ChainDetection.DetectChains(entities, tolerance: 1e-4);

            Assert.Single(chains);
        }

        [Fact]
        public void DetectChains_CirclesAreStandalone()
        {
            var line1 = new LineEntity(new Point2D(0, 0), new Point2D(1, 0));
            var circle = new CircleEntity(new Point2D(5, 5), 2);

            var entities = new List<EntityBase> { line1, circle };
            var chains = ChainDetection.DetectChains(entities);

            Assert.Equal(2, chains.Count);
        }

        [Fact]
        public void DetectChains_LineAndArc_SharedEndpoint_SameChain()
        {
            // Arc from 0° to 90° at origin, radius 1
            // Arc endpoint at 0° is (1, 0)
            var arc = new ArcEntity(new Point2D(0, 0), 1, 0, 90);
            var line = new LineEntity(new Point2D(1, 0), new Point2D(2, 0));

            var entities = new List<EntityBase> { arc, line };
            var chains = ChainDetection.DetectChains(entities, tolerance: 1e-4);

            Assert.Single(chains);
        }

        [Fact]
        public void GrowToChain_ReturnsFullChain()
        {
            var line1 = new LineEntity(new Point2D(0, 0), new Point2D(1, 0));
            var line2 = new LineEntity(new Point2D(1, 0), new Point2D(2, 0));
            var line3 = new LineEntity(new Point2D(5, 5), new Point2D(6, 5));

            var entities = new List<EntityBase> { line1, line2, line3 };
            var chainIds = ChainDetection.GrowToChain(line1.Id, entities);

            Assert.Equal(2, chainIds.Count);
            Assert.Contains(line1.Id, chainIds);
            Assert.Contains(line2.Id, chainIds);
            Assert.DoesNotContain(line3.Id, chainIds);
        }

        [Fact]
        public void IsSelectionClosed_ClosedTriangle_True()
        {
            var line1 = new LineEntity(new Point2D(0, 0), new Point2D(1, 0));
            var line2 = new LineEntity(new Point2D(1, 0), new Point2D(0.5, 1));
            var line3 = new LineEntity(new Point2D(0.5, 1), new Point2D(0, 0));

            var entities = new List<EntityBase> { line1, line2, line3 };
            var ids = entities.Select(e => e.Id);

            Assert.True(ChainDetection.IsSelectionClosed(ids, entities));
        }

        [Fact]
        public void IsSelectionClosed_OpenChain_False()
        {
            var line1 = new LineEntity(new Point2D(0, 0), new Point2D(1, 0));
            var line2 = new LineEntity(new Point2D(1, 0), new Point2D(2, 0));

            var entities = new List<EntityBase> { line1, line2 };
            var ids = entities.Select(e => e.Id);

            Assert.False(ChainDetection.IsSelectionClosed(ids, entities));
        }

        #endregion
    }
}
