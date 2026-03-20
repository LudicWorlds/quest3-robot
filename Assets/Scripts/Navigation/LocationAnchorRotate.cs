using UnityEngine;

public class LocationAnchorRotate : MonoBehaviour
{
    [SerializeField] private float m_degreesPerSecond = 30f;
    [SerializeField] private Vector3 m_axis = Vector3.up;

    void Update()
    {
        transform.Rotate(m_axis, m_degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
