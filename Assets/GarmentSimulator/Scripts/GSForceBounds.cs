using UnityEngine;

public class GSForceBounds: MonoBehaviour
{
    [SerializeField]
    private Transform _position;

    [SerializeField]
    private Vector3 _center;

    [SerializeField]
    private Vector3 _extents = new Vector3(0.01f, 0.01f, 0.01f);

    void Update()
    {
        if (!TryGetComponent<MeshRenderer>(out var renderer)) return;

        if (_position != null) {
            _center = _position.position;
        }

        renderer.bounds = new Bounds
        {
            center = _center,
            extents = _extents,
        };
    }
}
