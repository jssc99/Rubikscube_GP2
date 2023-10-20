using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;
using UnityEngine.EventSystems;
using System.Linq;
using System.Collections;

public class RubikBehaviour : MonoBehaviour
{
    private uint m_RubikSize = 3;
    private GameObject[] m_Cubes = new GameObject[0];
    [SerializeField] private GameObject m_CubePrefab = null;
    [SerializeField] private GameObject m_Camera = null;

    [Header("Materials")]
    [SerializeField] private Material m_MaterialFront = null;
    [SerializeField] private Material m_MaterialBack = null;
    [SerializeField] private Material m_MaterialTop = null;
    [SerializeField] private Material m_MaterialBottom = null;
    [SerializeField] private Material m_MaterialLeft = null;
    [SerializeField] private Material m_MaterialRight = null;


    [Header("Controls")]
    [SerializeField, Tooltip("Degree per pixel")] private float m_AngularSpeed = 20;
    [SerializeField, Tooltip("Pixels before turning")] private float m_deltaThreshold = 20;
    private bool m_BlockedCtrls = false;

    [Header("Plane Detector")]
    [SerializeField] private OnMouseOverColor m_PlaneDetectorScript;
    private MouseOverNormal m_MouseOverNormal;

    [Header("Auto controls")]
    [SerializeField, Tooltip("In seconds")] private float m_TimePerMoveToSolve = 0.5f;
    [SerializeField, Tooltip("In seconds")] private float m_TimePerMoveToShuffle = 0.25f;


    Dictionary<string, List<GameObject>> m_Faces;

    private GameObject m_SelectedFace = null;
    private GameObject m_SelectedCube = null;
    private List<GameObject> m_SelectedGroupCubes;

    private bool m_AxisDecided = false;
    private Vector2 m_PreviousPos = Vector2.zero;

    //Temp Move
    bool m_MouseXoverY;
    Vector3 m_FirstAxis;
    private int temp_axis;
    private int temp_index;
    private float temp_angleofrotation = 0;

    public event Action MovesChanged;
    //Setter
    private List<MoveClass> m_Moves;
    //UI Getter
    public List<MoveClass> moves
    {
        get { return m_Moves; }
    }

    private void Start()
    {
        if (m_CubePrefab == null || m_Camera == null)
            ErrorDetected("One or multiple field unset in RubikBehaviour");

        if (m_MaterialFront == null || m_MaterialBack == null ||
            m_MaterialTop == null || m_MaterialBottom == null ||
            m_MaterialLeft == null || m_MaterialRight == null)
            ErrorDetected("One or multiple texture missing in RubikBehaviour");

        m_SelectedGroupCubes = new List<GameObject>();
        m_Moves = new List<MoveClass>();
        m_MouseOverNormal = new MouseOverNormal();
        m_Faces = new();
        m_Faces["Front"] = new List<GameObject>();
        m_Faces["Back"] = new List<GameObject>();
        m_Faces["Bottom"] = new List<GameObject>();
        m_Faces["Top"] = new List<GameObject>();
        m_Faces["Left"] = new List<GameObject>();
        m_Faces["Right"] = new List<GameObject>();

        Reload(m_RubikSize, 0);
    }

    private void Update()
    {
        // Check if the mouse pointer is over a UI element
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            // Mouse is over a UI element, so ignore clicks
            return;
        }

        RotateFaceCtrl();
        RotateAllCtrl();
    }

    private void RotateAllCtrl()
    {
        if (Input.GetMouseButtonDown((int)MouseButton.RightMouse))
        {
            m_PreviousPos = Input.mousePosition;
        }
        else if (Input.GetMouseButton((int)MouseButton.RightMouse))
        {
            Vector2 newPos = Input.mousePosition;
            if (newPos == m_PreviousPos)
                return;
            Vector2 dir = m_PreviousPos - newPos;

            float angleXAxis = -dir.y * m_AngularSpeed / 180f;
            float angleYAxis = dir.x * m_AngularSpeed / 180f;

            RotateAll(angleXAxis, angleYAxis);

            m_PreviousPos = newPos;
        }
    }

    private void RotateAll(float _angleXAxis, float _angleYAxis)
    {
        Quaternion yAxis = Quaternion.AngleAxis(_angleYAxis, Vector3.up);
        Quaternion xAxis = Quaternion.AngleAxis(_angleXAxis, Vector3.right);

        transform.rotation = xAxis * yAxis * transform.rotation;
    }

    private int SelectAxis(bool _XoverY)
    {
        Vector3 axis;
        if (_XoverY)
            axis = m_SelectedFace.transform.up;
        else
            axis = m_SelectedFace.transform.right;

        //Bottom
        if (axis == transform.up)
        {
            temp_index = PositionToIndex(m_SelectedCube.transform.localPosition.y);
            SelectFace(1);
            return temp_axis = 1;
        }
        //Up
        if (axis == -transform.up)
        {
            temp_index = PositionToIndex(m_SelectedCube.transform.localPosition.y);
            SelectFace(1);
            return temp_axis = -1;
        }
        //Left
        if (axis == transform.right)
        {
            temp_index = PositionToIndex(m_SelectedCube.transform.localPosition.x);
            SelectFace(0);
            return temp_axis = 0;
        }
        //Right
        if (axis == -transform.right)
        {
            temp_index = PositionToIndex(m_SelectedCube.transform.localPosition.x);
            SelectFace(0);
            return temp_axis = -3;
        }
        //Bottom
        if (axis == transform.forward)
        {
            temp_index = PositionToIndex(m_SelectedCube.transform.localPosition.z);
            SelectFace(2);
            return temp_axis = 2;
        }
        //Up
        if (axis == -transform.forward)
        {
            temp_index = PositionToIndex(m_SelectedCube.transform.localPosition.z);
            SelectFace(2);
            return temp_axis = -2;
        }
        m_SelectedGroupCubes.Clear();
        return 0;
    }
    //index 0<->x, 1<->y, 2<->z 
    private void SelectFace(uint _axis)
    {
        if (_axis == 0)
        {
            foreach (GameObject cube in m_Cubes)
                if (cube.transform.localPosition.x == m_SelectedCube.transform.localPosition.x)
                    m_SelectedGroupCubes.Add(cube);
        }
        else if (_axis == 1)
        {
            foreach (GameObject cube in m_Cubes)
                if (cube.transform.localPosition.y == m_SelectedCube.transform.localPosition.y)
                    m_SelectedGroupCubes.Add(cube);
        }
        else if (_axis == 2)
        {
            foreach (GameObject cube in m_Cubes)
                if (cube.transform.localPosition.z == m_SelectedCube.transform.localPosition.z)
                    m_SelectedGroupCubes.Add(cube);
        }
    }
    private int PositionToIndex(float _pos)
    {
        return Mathf.RoundToInt(_pos + (m_RubikSize - 1) * 0.5f);

    }
    private float IndexToPosition(uint _index)
    {
        return _index - (m_RubikSize - 1) * 0.5f;
    }
    private void SelectFaceNumber(uint _axis, uint _index)
    {
        float pos = IndexToPosition(_index);
        if (_axis == 0)
        {
            foreach (GameObject cube in m_Cubes)
                if (cube.transform.localPosition.x == pos)
                    m_SelectedGroupCubes.Add(cube);
        }
        if (_axis == 1)
        {
            foreach (GameObject cube in m_Cubes)
                if (cube.transform.localPosition.y == pos)
                    m_SelectedGroupCubes.Add(cube);
        }
        if (_axis == 2)
        {
            foreach (GameObject cube in m_Cubes)
                if (cube.transform.localPosition.z == pos)
                    m_SelectedGroupCubes.Add(cube);
        }
    }
    private void RotateFaceCtrl()
    {
        //On click
        if (Input.GetMouseButtonDown((int)MouseButton.LeftMouse))
        {
            m_MouseOverNormal.Update();
            if (m_MouseOverNormal.selectedObject == null || m_BlockedCtrls)
                return;
            //SelectedObject is a face, so we take its parent to get the Cube
            m_SelectedFace = m_MouseOverNormal.selectedObject;
            m_SelectedCube = m_MouseOverNormal.selectedObject.transform.parent.gameObject;
            m_PreviousPos = Input.mousePosition;
        }
        //On hold
        else if (Input.GetMouseButton((int)MouseButton.LeftMouse))
        {
            //if clicked outside
            if (m_SelectedCube == null)
                return;
            Vector2 newPos = Input.mousePosition;
            if (newPos == m_PreviousPos)
                return;
            Vector2 mouseDir = m_PreviousPos - newPos;

            //Shortcut by Unity
            if (!m_AxisDecided)
            {
                //test over squared distance
                if (Vector3.SqrMagnitude(mouseDir) > m_deltaThreshold * m_deltaThreshold)
                {
                    //follow the axis of the face
                    if (Mathf.Abs(Vector3.Dot(mouseDir, m_SelectedFace.transform.right)) >= Mathf.Abs(Vector3.Dot(mouseDir, m_SelectedFace.transform.up)))
                    {
                        m_FirstAxis = m_SelectedFace.transform.right;
                        m_MouseXoverY = false;

                    }
                    else
                    {
                        m_FirstAxis = m_SelectedFace.transform.up;
                        m_MouseXoverY = true;
                    }
                }
                else
                    return;
                temp_axis = SelectAxis(!m_MouseXoverY);
                m_AxisDecided = true;
            }
            float deltangle = (temp_axis < +0 ? -1 : 1) * (m_MouseXoverY ? -Vector3.Dot(mouseDir, m_FirstAxis) : Vector3.Dot(mouseDir, m_FirstAxis)) * m_AngularSpeed / 90;
            temp_angleofrotation += deltangle;

            //Precise here X or Y
            RotateFace((uint)Mathf.Abs(temp_axis) % 3, (uint)temp_index, deltangle);

            m_PreviousPos = newPos;
        }
        //On release
        else
        {
            if (temp_angleofrotation != 0f)
            {
                int moves = Mathf.RoundToInt(temp_angleofrotation / 90f) % 4;//4 rotations = back to start
                RotateFace((uint)Mathf.Abs(temp_axis) % 3, (uint)temp_index, -temp_angleofrotation //reset rotation
                                                                         + moves * 90f);       //Clip to nearest angle

                RoundFacePositions((m_RubikSize % 2) == 0);
                //Here add 1 more move to list
                if (moves != 0)
                    AddMove((uint)Mathf.Abs(temp_axis) % 3, (uint)temp_index, moves);
            }
            //Reset Variables
            temp_angleofrotation = 0f;
            temp_axis = 0;
            m_SelectedFace = null;
            m_SelectedCube = null;
            m_AxisDecided = false;
            m_SelectedGroupCubes.Clear();
            CheckCompletionByFace();
        }
    }
    private void RotateFace(Vector3 _axis, float _angle)
    {
        foreach (GameObject cube in m_SelectedGroupCubes)
        {
            Vector3 oldposition = cube.transform.position;
            Quaternion currentRotation = Quaternion.AngleAxis(_angle, _axis);
            Quaternion newposition = currentRotation * new Quaternion(oldposition.x, oldposition.y, oldposition.z, 0f) * Quaternion.Inverse(currentRotation);
            cube.transform.position = new Vector3(newposition.x, newposition.y, newposition.z);
            cube.transform.rotation = currentRotation * cube.transform.rotation;
        }
    }
    private void RotateFace(uint _axis, uint _index, float _angle)
    {
        Vector3 axis;
        switch (_axis)
        {
            case 0:
                axis = transform.right;
                break;
            case 1:
                axis = transform.up;
                break;
            case 2:
                axis = transform.forward;
                break;
            default:
                return;
        }
        RotateFace(axis, _angle);
    }
    //Rotate a fixed number of 90deg rotations
    private void RotateFace(uint _axis, uint _index, int _number)
    {
        SelectFaceNumber(_axis, _index);
        RotateFace(_axis, _index, (float)_number * 90f);
        RoundFacePositions((m_RubikSize % 2) == 0);
        m_SelectedGroupCubes.Clear();
    }
    private void RoundFacePositions(bool _evenNumberOfFaces)
    {
        foreach (GameObject cube in m_SelectedGroupCubes)
        {
            Vector3 oldposition = cube.transform.localPosition;
            if (_evenNumberOfFaces)
                oldposition -= new Vector3(0.5f, 0.5f, 0.5f);
            Vector3 newposition = new Vector3(Mathf.RoundToInt(oldposition.x), Mathf.RoundToInt(oldposition.y), Mathf.RoundToInt(oldposition.z));
            if (_evenNumberOfFaces)
                newposition += new Vector3(0.5f, 0.5f, 0.5f);
            cube.transform.localPosition = newposition;
        }
    }
    private void AddMove(uint _axis, uint _index, int _number, bool skipRedundancyAnalysis = false)
    {
        if (m_Moves.Count != 0 && m_Moves.Last().axis == _axis && m_Moves.Last().index == _index)
        {
            m_Moves.Last().number = (m_Moves.Last().number + _number) % 4;
            if (m_Moves.Last().number == 0)
            {
                m_Moves.RemoveAt(m_Moves.Count - 1);
            }
        }
        else
        {
            m_Moves.Add(new MoveClass(_axis, _index, _number));
        }
        if (!skipRedundancyAnalysis)
            CyclicRemove();
        MovesChanged?.Invoke();
    }
    // A whole row/colon/aisle turned by the same amount = no change
    private void CyclicRemove()
    {
        if (m_Moves.Count < m_RubikSize)
            return;
        List<MoveClass> lastXmoves = new()
        {
            m_Moves.Last()
        };
        int min = m_Moves.Last().number;
        for (int i = 1; i < m_Moves.Count && i < m_RubikSize; i++)
        {
            if (m_Moves.Count < 2)
                return;
            MoveClass i_before_last = m_Moves[m_Moves.Count - 1 - i];        //must have

            if (i_before_last.axis != m_Moves.Last().axis)     //same axis
            {
                lastXmoves.Clear(); //not necessary but keeping good habits
                return;
            }
            bool add = true;
            for (int j = lastXmoves.Count - 1; j >= 0; j--)
            {
                MoveClass lasts = lastXmoves[j];
                // Here merge same moves
                if (i_before_last.index == lasts.index) // Different indexes should always be true
                {
                    lasts.number = (lasts.number + i_before_last.number) % 4;
                    if (lasts.number == 0)
                    {
                        lastXmoves.RemoveAt(j);
                        m_Moves.Remove(lasts);
                        i--;
                    }
                    m_Moves.Remove(i_before_last);
                    i--;
                    add = false;
                    break;
                }
            }
            if (!add)
                continue;
            lastXmoves.Add(i_before_last);
            if (i_before_last.number < min)
                min = i_before_last.number;
        }
        if (m_Moves.Count != m_RubikSize)
            return;
        // erase moves the more the better
        for (int i = lastXmoves.Count - 1; i >= 0; i--)
        {
            MoveClass lasts = lastXmoves[i];
            lasts.number = (lasts.number - min) % 4;
            if (lasts.number == 0)
            {
                int indexToRemove = m_Moves.Count - 1 - lastXmoves.IndexOf(lasts);
                m_Moves.RemoveAt(indexToRemove);
                lastXmoves.RemoveAt(i);
            }
            else
            {
                int indexToModify = m_Moves.Count - 1 - lastXmoves.IndexOf(lasts);
                m_Moves[indexToModify].number = lasts.number;
            }
        }
        lastXmoves.Clear();
        CyclicRemove();
    }

    private void HandleMouseOver()
    {
        // This method will be called when a child face is moused over.

    }

    private void ErrorDetected(string _error)
    {
        Debug.LogError(_error);
#if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
#endif
        Application.Quit();
    }

    public void Reload(uint _newSize, uint _shuffles)
    {
        DestroyRubik();
        m_RubikSize = _newSize;
        m_Camera.GetComponent<CameraBehaviour>().SizeChanged(_newSize);
        //Rotate to see 3 faces
        CreateRubik();
        Shuffle(_shuffles);
        RotateAll(-30, 45);
    }

    private void DestroyRubik()
    {
        if (m_Cubes.Length > 0)
            for (uint id = 0; id < m_Cubes.Length; id++)
                Destroy(m_Cubes[id]);

        m_Cubes = new GameObject[0];
        transform.rotation = Quaternion.identity;
        m_Moves.Clear();
        foreach (var face in m_Faces)
            face.Value.Clear();
        StopAllCoroutines();
    }

    private void CreateRubik()
    {
        uint cubesInFace = m_RubikSize * m_RubikSize;
        uint cubesToMake = cubesInFace * m_RubikSize;
        m_Cubes = new GameObject[cubesToMake];

        float limit = .5f * (m_RubikSize - 1);
        float x = -limit, y = -limit, z = -limit;
        for (uint id = 0; id < cubesToMake; id++)
        {
            m_Cubes[id] = Instantiate(m_CubePrefab, transform);
            m_Cubes[id].name = "Cube_" + id;

            if ((id % cubesInFace) == 0) z++;
            if (z > limit) z = -limit;

            if ((id % m_RubikSize) == 0) y++;
            if (y > limit) y = -limit;

            x++;
            if (x > limit) x = -limit;

            m_Cubes[id].transform.position = new Vector3(x, y, z);

            Transform extFace;
            if (z == -limit) // front face
            {
                extFace = m_Cubes[id].transform.Find("Front");
                extFace.GetComponent<MeshRenderer>().material = m_MaterialFront;
                OnMouseOverColor script = extFace.gameObject.AddComponent(typeof(OnMouseOverColor)) as OnMouseOverColor;
                script.onMouseOverAction += HandleMouseOver;
                extFace.gameObject.tag = "ext";
                m_Faces["Front"].Add(extFace.gameObject);
            }
            else if (z == limit) // back face
            {
                extFace = m_Cubes[id].transform.Find("Back");
                extFace.GetComponent<MeshRenderer>().material = m_MaterialBack;
                OnMouseOverColor script = extFace.gameObject.AddComponent(typeof(OnMouseOverColor)) as OnMouseOverColor;
                script.onMouseOverAction += HandleMouseOver;
                extFace.gameObject.tag = "ext";
                m_Faces["Back"].Add(extFace.gameObject);
            }
            if (y == -limit) // bottom face
            {
                extFace = m_Cubes[id].transform.Find("Bottom");
                extFace.GetComponent<MeshRenderer>().material = m_MaterialBottom;
                OnMouseOverColor script = extFace.gameObject.AddComponent(typeof(OnMouseOverColor)) as OnMouseOverColor;
                script.onMouseOverAction += HandleMouseOver;
                m_Faces["Bottom"].Add(extFace.gameObject);
                extFace.gameObject.tag = "ext";
            }
            else if (y == limit) // top face
            {
                extFace = m_Cubes[id].transform.Find("Top");
                extFace.GetComponent<MeshRenderer>().material = m_MaterialTop;
                OnMouseOverColor script = extFace.gameObject.AddComponent(typeof(OnMouseOverColor)) as OnMouseOverColor;
                script.onMouseOverAction += HandleMouseOver;
                extFace.gameObject.tag = "ext";
                m_Faces["Top"].Add(extFace.gameObject);
            }
            if (x == -limit) // left face
            {
                extFace = m_Cubes[id].transform.Find("Left");
                extFace.GetComponent<MeshRenderer>().material = m_MaterialLeft;
                OnMouseOverColor script = extFace.gameObject.AddComponent(typeof(OnMouseOverColor)) as OnMouseOverColor;
                script.onMouseOverAction += HandleMouseOver;
                extFace.gameObject.tag = "ext";
                m_Faces["Left"].Add(extFace.gameObject);
            }
            else if (x == limit) // right face
            {
                extFace = m_Cubes[id].transform.Find("Right");
                extFace.GetComponent<MeshRenderer>().material = m_MaterialRight;
                OnMouseOverColor script = extFace.gameObject.AddComponent(typeof(OnMouseOverColor)) as OnMouseOverColor;
                script.onMouseOverAction += HandleMouseOver;
                extFace.gameObject.tag = "ext";
                m_Faces["Right"].Add(extFace.gameObject);
            }
        }
    }
    //Use after Addmoves for more efficiency
    private void CheckCompletionByFace()
    {
        foreach (var face in m_Faces)
        {
            Vector3 fwdVector = face.Value.First().transform.forward;
            foreach (var subsquare in face.Value)
            {
                if (subsquare.transform.forward != fwdVector)
                    return;
            }
        }
        if (m_Moves.Count != 0)
        {
            m_Moves.Clear();
            MovesChanged?.Invoke();
        }
    }

    private void Shuffle(uint _number)
    {
        StartCoroutine(ShuffleWithDelay(_number));
    }
    private IEnumerator ShuffleWithDelay(uint _number)
    {
        while (m_Moves.Count() < _number)
        {
            uint axis = (uint)UnityEngine.Random.Range(0, 2);
            uint index = (uint)UnityEngine.Random.Range(0, m_RubikSize - 1);
            int number = UnityEngine.Random.Range(1, 3);
            RotateFace(axis, index, number);
            AddMove(axis, index, number);
            m_BlockedCtrls = true;
            yield return new WaitForSeconds(m_TimePerMoveToShuffle); // Adjust the time as needed
        }
        m_BlockedCtrls = false;
    }
    public void Solve()
    {
        StartCoroutine(SolveWithDelay());
    }

    private IEnumerator SolveWithDelay()
    {
        while (m_Moves.Count() > 0)
        {
            uint axis = m_Moves.Last().axis;
            uint index = m_Moves.Last().index;
            int number = -m_Moves.Last().number;

            // Rotate the face
            RotateFace(axis, index, number);
            // Add the move
            AddMove(axis, index, number, true);
            m_BlockedCtrls = true;
            // Wait for a specified amount of time (e.g., 1 second)
            yield return new WaitForSeconds(m_TimePerMoveToSolve); // Adjust the time as needed
        }
        m_BlockedCtrls = false;
    }
}
