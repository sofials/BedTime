using UnityEngine;

public class MovePivotOnSpline : MonoBehaviour
{
    public Transform point0;
    public Transform point1;
    public Transform point2;
    public Transform point3;

    [Range(0f, 1f)]
    public float t;

    public float speed = 0.2f; // velocità movimento spline

    void Update()
    {
        t += speed * Time.deltaTime;
        if (t > 1f)
            t -= 1f;

        transform.position = GetBezierPoint(t, point0.position, point1.position, point2.position, point3.position);

        // Optional: puoi aggiungere rotazione in base alla tangente spline per orientare il pivot
    }

    Vector3 GetBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 point = uuu * p0;
        point += 3 * uu * t * p1;
        point += 3 * u * tt * p2;
        point += ttt * p3;

        return point;
    }
}