using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

public class RaftPlatform : MonoBehaviour
{
    public SplineContainer splineContainer;
    public float speed = 10f;
    public int sampleResolution = 100;

    private List<Vector3> sampledPoints = new List<Vector3>();
    private List<float> cumulativeDistances = new List<float>();
    private float currentDistance = 0f;
    private float totalLength = 0f;
    private int direction = 1;

    private Vector3 lastPosition;
    private CharacterController playerController = null;

    private float speedMultiplier = 1f;

    private Vector3 deltaMovement;

    public Vector3 DeltaMovement => deltaMovement;

    private bool isMoving = false;
    private bool isWaitingAtEnd = false;
    private bool wasMovingWithPlayer = false;

    private float startDistance = 0f;
    private float endDistance = 0f;

    void Start()
    {
        SampleSpline();
        lastPosition = transform.position;

        // Calcola la distanza tra knot 0 e 1
        startDistance = 0f;
        endDistance = GetDistanceAtT(1f / (splineContainer.Spline.Count - 1));
        currentDistance = startDistance;

        isWaitingAtEnd = true;
    }

    void Update()
    {
        if (isMoving)
        {
            currentDistance += speed * speedMultiplier * direction * Time.deltaTime;

            if (direction == 1 && currentDistance >= endDistance)
            {
                currentDistance = endDistance;
                isMoving = false;
                isWaitingAtEnd = true;
            }
            else if (direction == -1 && currentDistance <= startDistance)
            {
                currentDistance = startDistance;
                isMoving = false;
                isWaitingAtEnd = true;
            }

            Vector3 newPosition = GetPositionAtDistance(currentDistance);
            deltaMovement = newPosition - lastPosition;
            transform.position = newPosition;
            lastPosition = newPosition;
        }
        else
        {
            deltaMovement = Vector3.zero;
            lastPosition = transform.position;

            // Se il player era a bordo e ora è morto, torna al capolinea
            if (wasMovingWithPlayer && playerController == null)
            {
                isMoving = true;
                isWaitingAtEnd = false;
                direction = (Mathf.Abs(currentDistance - startDistance) < Mathf.Abs(currentDistance - endDistance)) ? -1 : 1;
                Debug.Log("[RaftPlatform] Player scomparso, torno al capolinea.");
                wasMovingWithPlayer = false;
            }
        }
    }

    void SampleSpline()
    {
        sampledPoints.Clear();
        cumulativeDistances.Clear();

        totalLength = 0f;
        Vector3 prevPoint = splineContainer.EvaluatePosition(0f);
        sampledPoints.Add(prevPoint);
        cumulativeDistances.Add(0f);

        for (int i = 1; i <= sampleResolution; i++)
        {
            float t = (float)i / sampleResolution;
            Vector3 point = splineContainer.EvaluatePosition(t);
            float dist = Vector3.Distance(prevPoint, point);
            totalLength += dist;

            sampledPoints.Add(point);
            cumulativeDistances.Add(totalLength);
            prevPoint = point;
        }
    }

    float GetDistanceAtT(float t)
    {
        float targetDistance = t * totalLength;
        for (int i = 1; i < cumulativeDistances.Count; i++)
        {
            if (cumulativeDistances[i] >= targetDistance)
            {
                return cumulativeDistances[i];
            }
        }
        return totalLength;
    }

    Vector3 GetPositionAtDistance(float distance)
    {
        if (distance <= 0f) return sampledPoints[0];
        if (distance >= totalLength) return sampledPoints[sampledPoints.Count - 1];

        for (int i = 1; i < cumulativeDistances.Count; i++)
        {
            if (cumulativeDistances[i] >= distance)
            {
                float prevDist = cumulativeDistances[i - 1];
                float nextDist = cumulativeDistances[i];
                float segmentT = Mathf.InverseLerp(prevDist, nextDist, distance);
                return Vector3.Lerp(sampledPoints[i - 1], sampledPoints[i], segmentT);
            }
        }

        return sampledPoints[sampledPoints.Count - 1];
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
        Debug.Log($"[RaftPlatform] {gameObject.name} speed multiplier impostato a {multiplier}");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerController = other.GetComponent<CharacterController>();

            // Riparte quando il player sale, anche se era ferma
            if (isWaitingAtEnd || !isMoving)
            {
                isMoving = true;
                isWaitingAtEnd = false;

                // Imposta direzione corretta per ripartire dall'estremo
                if (Mathf.Approximately(currentDistance, startDistance))
                    direction = 1; // Verso end
                else if (Mathf.Approximately(currentDistance, endDistance))
                    direction = -1; // Torna indietro
                                    // Altrimenti mantiene la direzione attuale

                wasMovingWithPlayer = true;
                Debug.Log("[RaftPlatform] Player salito sopra, riparto.");
            }
        }
    }


    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (playerController == other.GetComponent<CharacterController>())
            {
                // Il player è saltato via, fermati subito (ma riparte se risale)
                Debug.Log("[RaftPlatform] Player saltato via, fermo la piattaforma.");
                playerController = null;
                isMoving = false;
                wasMovingWithPlayer = false;
            }
        }
    }
}
