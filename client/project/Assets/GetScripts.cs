
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.VisualScripting;

public class SimulationClient : MonoBehaviour
{
    private const string baseUrl = "http://localhost:8585";

    public GameObject robotPrefab;
    public GameObject boxPrefab;
    private Dictionary<int, GameObject> robots = new Dictionary<int, GameObject>();
    private Dictionary<int, Vector3> robots_prev_pos = new Dictionary<int, Vector3>();
    private Dictionary<int, GameObject> boxes = new Dictionary<int, GameObject>();
    private Dictionary<int, Vector3> robotVelocities = new Dictionary<int, Vector3>();
    public float minSpeed = 1f;
    public float maxSpeed = 5f;

    public float boxHeight = 0.5f;
    public float robotHeight = 1f;
    public float grabHeight = 0.5f;

    void Start()
    {
        StartCoroutine(GetSimulationState());
    }

    IEnumerator GetSimulationState()
    {
        while (true)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get(baseUrl))
            {
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = webRequest.downloadHandler.text;
                    ProcessSimulationState(jsonResponse);
                }
                else
                {
                    Debug.LogError("Error: " + webRequest.error);
                }
            }

            yield return new WaitForSeconds(1f);
        }
    }

    void ProcessSimulationState(string jsonResponse)
    {
        JObject state = JObject.Parse(jsonResponse);

        HashSet<int> activeBoxIds = new HashSet<int>();

        ProcessBoxPositions(state["box_positions"], activeBoxIds);

        ProcessRobotActions(state["robot_actions"], activeBoxIds);

        DeactivateInactiveBoxes(activeBoxIds);
    }

    void ProcessBoxPositions(JToken boxPositions, HashSet<int> activeBoxIds)
    {
        foreach (var boxPosition in boxPositions)
        {
            int id = (int)boxPosition["id"];
            activeBoxIds.Add(id);  // Mark this box as active

            Vector3 position = new Vector3((float)boxPosition["position"][0], 0, (float)boxPosition["position"][1]);
            string action = (string)boxPosition["action"];
            int numBoxes = (int)boxPosition["num_boxes"];

            if (!boxes.ContainsKey(id))
            {
                GameObject newBox = Instantiate(boxPrefab, position, Quaternion.identity);
                boxes[id] = newBox;
            }

            GameObject box = boxes[id];
            box.SetActive(true);  // Ensure the box is active

            // Only update position if the box is not being carried by a robot
            if (box.transform.parent == null)
            {
                box.transform.position = position;

                // Adjust the box's appearance based on the number of boxes in the stack
                box.transform.localScale = new Vector3(1, numBoxes * boxHeight, 1);

                if (action == "stacked")
                {
                    // Position the box at the center of the stack
                    float stackHeight = numBoxes * boxHeight;
                    box.transform.position = new Vector3(position.x, stackHeight / 2, position.z);
                }
            }
        }
    }

    IEnumerator InterpolationAction(int id, Vector3 targetPosition)
    {
        var robot = robots[id];
        Vector3 startPosition = robot.transform.position;
        float journeyLength = Vector3.Distance(startPosition, targetPosition);
        float startTime = Time.time;

        while (robot.transform.position != targetPosition)
        {
            float distanceCovered = (Time.time - startTime) * robotVelocities[id].magnitude; // how much distance has been covered by the robot
            float fractionOfJourney = distanceCovered / journeyLength;
            Vector3 newPosition = Vector3.Lerp(startPosition, targetPosition, fractionOfJourney);

            if (!CheckCollision(robot, newPosition))
            {
                robot.transform.position = newPosition;
            }
            else
            {
                // If there's a collision, 
                // generate a new random velocity and restart the journey
                robotVelocities[id] = GenerateRandomVelocity();
                startPosition = robot.transform.position;
                journeyLength = Vector3.Distance(startPosition, targetPosition);
                startTime = Time.time;
            }

            yield return null;
        }

        robots_prev_pos[id] = robot.transform.position;
    }

    bool CheckCollision(GameObject robot, Vector3 newPosition)
    {
        Vector3 direction = newPosition - robot.transform.position;
        float distance = direction.magnitude;

        RaycastHit hit;
        if (Physics.Raycast(robot.transform.position, direction.normalized, out hit, distance))
        {
            if (hit.collider.gameObject.CompareTag("Box"))
            {
                return true;
            }
        }

        return false;
    }

    Vector3 GenerateRandomVelocity()
    {
        float speed = Random.Range(minSpeed, maxSpeed);
        Vector3 randomDirection = Random.insideUnitSphere;
        randomDirection.y = 0; // Keep it on the horizontal plane // don't move up or down the plane 
        return randomDirection.normalized * speed;
    }

    void ProcessRobotActions(JToken robotActions, HashSet<int> activeBoxIds)
    {
        foreach (var robotAction in robotActions)
        {
            int id = (int)robotAction["id"];
            Vector3 position = new Vector3((float)robotAction["position"][0], 0, (float)robotAction["position"][1]);
            Vector3 direction = new Vector3((float)robotAction["direction"][0], 0, (float)robotAction["direction"][1]);
            string action = (string)robotAction["action"];
            int? boxId = robotAction["box_id"].Type != JTokenType.Null ? (int?)robotAction["box_id"] : null;

            if (boxId.HasValue)
            {
                activeBoxIds.Add(boxId.Value);
            }

            if (!robots.ContainsKey(id))
            {
                GameObject newRobot = Instantiate(robotPrefab, position, Quaternion.identity);
                robots[id] = newRobot;
                robots_prev_pos[id] = position;
                robotVelocities[id] = GenerateRandomVelocity();
            }

            GameObject robot = robots[id];
            StartCoroutine(InterpolationAction(id, position));
            robot.transform.forward = direction;

            // Handle different actions
            switch (action)
            {
                case "move":
                    // The robot's position has already been updated
                    break;
                case "turn random":
                    // The robot's direction has already been updated
                    break;
                case "grab":
                    if (boxId.HasValue && boxes.ContainsKey(boxId.Value))
                    {
                        GameObject box = boxes[boxId.Value];
                        box.transform.SetParent(robot.transform);
                        box.transform.localPosition = new Vector3(0, robotHeight + grabHeight, 0);

                    }
                    break;
                case "stack":
                    DeactivateBoxOnRobot(robot);
                    break;
            }
        }
    }

    void DeactivateInactiveBoxes(HashSet<int> activeBoxIds)
    {
        foreach (var boxEntry in boxes)
        {
            int boxId = boxEntry.Key;
            GameObject box = boxEntry.Value;

            if (!activeBoxIds.Contains(boxId))
            {
                box.SetActive(false);
                box.transform.SetParent(null);  // Unparent the box if it was attached to a robot
            }
        }
    }

    void DeactivateBoxOnRobot(GameObject robot)
    {
        for (int i = robot.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = robot.transform.GetChild(i);
            if (child.CompareTag("Box"))
            {
                child.gameObject.SetActive(false);
                child.SetParent(null); // Unparent the box
            }
        }
    }


}
