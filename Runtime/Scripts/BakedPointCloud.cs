// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

using UnityEngine;
using System.Collections.Generic;

namespace StoryLabResearch.PointCloud
{
    /// A container class for texture-baked point clouds.
    public sealed class BakedPointCloud : ScriptableObject
    {
        #region Public properties

        /// Number of points
        public int pointCount { get { return _pointCount; } }

        /// Position map texture
        public Texture2D positionMap { get { return _positionMap; } }

        /// Color map texture
        public Texture2D colorMap { get { return _colorMap; } }

        // Bounds
        public Bounds bounds { get { return _bounds; } }

        #endregion

        #region Serialized data members

        [SerializeField] int _pointCount;
        [SerializeField] Texture2D _positionMap;
        [SerializeField] Texture2D _colorMap;
        [SerializeField] Bounds _bounds;

        #endregion

        #region Editor functions

#if UNITY_EDITOR

        public void Initialize(string prefix, List<Vector3> positions, List<Color32> colors)
        {
            _pointCount = positions.Count;

            var width = Mathf.CeilToInt(Mathf.Sqrt(_pointCount));

            _positionMap = new Texture2D(width, width, TextureFormat.RGBAHalf, false);
            _positionMap.name = prefix + "Position Map";
            _positionMap.filterMode = FilterMode.Point;

            _colorMap = new Texture2D(width, width, TextureFormat.RGBA32, false);
            _colorMap.name = prefix + "Color Map";
            _colorMap.filterMode = FilterMode.Point;

            var i1 = 0;
            var i2 = 0U;

            // initialise min and max for bounds to first point, since we know this will be within the bounds
            Vector3 max = positions[0];
            Vector3 min = positions[0];

            for (var y = 0; y < width; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = i1 < _pointCount ? i1 : (int)(i2 % _pointCount);
                    var p = positions[i];

                    max.x = Mathf.Max(p.x, max.x);
                    max.y = Mathf.Max(p.y, max.y);
                    max.z = Mathf.Max(p.z, max.z);

                    min.x = Mathf.Min(p.x, min.x);
                    min.y = Mathf.Min(p.y, min.y);
                    min.z = Mathf.Min(p.z, min.z);

                    _positionMap.SetPixel(x, y, new Color(p.x, p.y, p.z));
                    _colorMap.SetPixel(x, y, colors[i]);

                    i1++;
                    i2 += 132049U; // prime
                }
            }

            _positionMap.Apply(false, true);
            _colorMap.Apply(false, true);

            _bounds = new Bounds
            {
                center = (max + min) / 2,
                size = max - min
            };
        }

#endif

        #endregion
    }
}