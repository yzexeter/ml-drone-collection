using UnityEngine;

public class NavAgent : DroneAgent
{
    public Target Target;
    public bool HasReachedTarget => targetDistance < 0.25f;

    protected enum DetectionMode
    {
        Raycast = 0,
        Camera = 1
    }

    [SerializeField]
    protected DetectionMode detectionMode;
    protected RayDetection rayDetection;
    protected Camera cam;

    protected float targetDistance;
    protected Vector3 targetDirection;
    protected Vector2 targetPolarAngle;

    protected int decisionInterval;
    protected int crntStep;

    protected IDrone drone;
    [SerializeField]
    private RotorCtrlAgent rotorCtrlAgent;

    public override void InitializeAgent()
    {
        drone = (IDrone)rotorCtrlAgent;

        if (detectionMode == DetectionMode.Raycast)
        {
            rayDetection = new RayDetection();
        }
        else
        {
            cam = GetComponentInChildren<Camera>();
            Texture2D tex = new Texture2D(84, 84, TextureFormat.RGB24, false);
            cam.GetComponent<DepthCam>().Initialize(ref tex);
            // TODO add tex to agent observations.
        }
    }

    public override void AgentReset()
    {
        RequestDecision();
    }

    public override void CollectObservations()
    {
        UpdateTargetObs();

        AddVectorObs(targetPolarAngle / 180f); // 2
        AddVectorObs(Util.Sigmoid(targetDistance) * 2f - 1f); // 1
        AddVectorObs(NormalizeSpeed(drone.CrntSpeed)); // 1
        AddVectorObs(drone.CrntDir.y); // 1 pitch

        if (detectionMode == DetectionMode.Raycast)
        {
            AddVectorObs(rayDetection.CastRays(drone, 10f));
        }
        else
        {
            cam.transform.position = drone.Transform.position - drone.CrntDir;
            cam.transform.rotation = Quaternion.LookRotation(drone.CrntDir);
        }
    }

    public override void AgentAction(float[] vectorAction, string textAction)
    {
        Vector2 polarAngle = new Vector2(vectorAction[0] * 180f, vectorAction[1] * 180f);
        drone.UpdateMotion(polarAngle, ScaleSpeed(vectorAction[2]));
        drone.UpdateAxes();

        decisionInterval = Mathf.RoundToInt((vectorAction[3] + 1f) * 10f) + 1; // -> 1-21
        crntStep = 0;
    }

    protected void UpdateTargetObs()
    {
        Vector3 perp = Vector3.Cross(drone.CrntDir, Vector3.up);
        targetDistance = Target.Distance(drone.Transform.position);
        targetDirection = Target.Direction(drone.Transform.position);
        targetPolarAngle.x = SignedAnglePlane(drone.CrntDir, targetDirection, perp);
        targetPolarAngle.y = SignedAnglePlane(drone.CrntDir, targetDirection, Vector3.up);
    }

    protected virtual void OnUpdate()
    {
        if (crntStep == decisionInterval)
        {
            RequestDecision();
        }
    }

    private void FixedUpdate()
    {
        crntStep++;
        OnUpdate();
    }

    private static float SignedAnglePlane(Vector3 v1, Vector3 v2, Vector3 plane)
    {
        return Vector3.SignedAngle(
            Vector3.ProjectOnPlane(v1, plane).normalized,
            Vector3.ProjectOnPlane(v2, plane).normalized,
            plane
        );
    }
}