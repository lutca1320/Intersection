﻿using System.Collections;
using Dreamteck.Splines;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using ArrayUtility = Dreamteck.ArrayUtility;

public class Crossroad
{
    private Vector3 position;
    private List<SplineComputer> roads = new List<SplineComputer>();

    public List<SplineComputer> getRoads()
    {
        return roads;
    }

    public Vector3 getPosition()
    {
        return position;
    }

    public void AddRoad(SplineComputer road)
    {
        if (!roads.Contains(road))
            roads.Add(road);
        else
            UnityEngine.Debug.LogWarning("DUP");
    }

    public void RemoveRoad(SplineComputer road)
    {
        roads.Remove(road);
    }

    public void SetRoads(SplineComputer[] list)
    {
        if (list != null)
        {
            roads.Clear();
            roads = list.ToList();
        }
    }

    public void SetRoads(List<SplineComputer> list)
    {
        if (list != null)
        {
            roads.Clear();
            roads = list;
        }
    }

    public void SetPosition(Vector3 pos)
    {
        position = pos;
    }

    public void Update()
    {
    }
}

public class CreatePathManager : MonoBehaviour
{
    public enum MODE
    {
        BUILD,
        APPEND,
        REMOVE,
        NONE
    };

    public enum JOINMODE
    {
        TO3,
        TO4,
        HEAD,
        TO3_SPLIT,
        NONE
    };

    public enum ROADLANE
    {
        RL1,
        RL2,
        RL3,
        RL4
    };

    private enum MERGEMODE
    {
        LL,
        FF,
        LF,
        FL,
        LOOP,
        NONE
    };

    public SplineComputer[] roadPrefabs;

    private Camera cm;
    public SplineComputer SplinePrefab;
    public GameObject debugobj;
    public GameObject debugObj_2;

    public GameObject debugObj_3;
    public GameObject debugObj_4;
    public GameObject textObj;

    public int snapsize = 10;
    private Vector3 def_normal = new Vector3(0, 1, 0);
    private float def_y = 0.0f;
    public float divider = 7.2f;

    public SplineComputer current_spline;
    public MODE current_mode = MODE.NONE;
    private JOINMODE joinmode = JOINMODE.NONE;
    public int new_index = 0;
    public ROADLANE currentRoadLane = ROADLANE.RL1;

    public float last_x;
    public float last_z;
    public Vector3 last_pos;
    private Vector3 pos;
    public Vector3 snap_pos;

    public SplineComputer selected_spline;
    public List<SplineComputer> selectedSplines;
    public int selected_index = 0;
    public Crossroad selected_crossroad;
    public SplineComputer cross_old_spline;
    public SplineComputer cross_new_spline;
    
    public List<GameObject> texts = new List<GameObject>();

    public List<Crossroad> crossroads = new List<Crossroad>();

    void debugPoint(Vector3 pos)
    {
        Instantiate(debugObj_2, pos, Quaternion.identity);
    }

    void debugVector(Vector3 start, Vector3 end)
    {
        Instantiate(debugObj_3, start, Quaternion.identity);
        Instantiate(debugObj_4, end, Quaternion.identity);
    }

    public void LogTextOnPos(string text, Vector3 pos)
    {
        GameObject obj;
        if (texts.FirstOrDefault(o => (o.transform.position == pos) && (o.GetComponent<TextMesh>().text != text)) != null)
        {
            obj = Instantiate(textObj, pos - new Vector3(0, 0, 1), Quaternion.Euler(90, 0, 0));
        }
        else
        {
            obj = Instantiate(textObj, pos, Quaternion.Euler(90, 0, 0));
        }
        obj.GetComponent<TextMesh>().text = text;
        obj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        texts.Add(obj);
        
        StartCoroutine(Stop());

        IEnumerator Stop()
        {
            yield return 0;
            texts.Remove(obj);
            Destroy(obj);
        }
    }

    float SnapGrid(float value, int snapsize)
    {
        if (value < 0)
        {
            return Mathf.Round(Mathf.Abs(value / snapsize)) * snapsize * -1;
        }
        else
        {
            return Mathf.Round(value / snapsize) * snapsize;
        }
    }

    Vector3 SnapToGridPoint(Vector3 pos, int _snapsize)
    {
        var snapsize = (float) _snapsize;

        if (!isVectorInXZArea(pos, -snapsize + last_pos.x, snapsize + last_pos.x,
            -snapsize + last_pos.z, snapsize + +last_pos.z))
        {
            UnityEngine.Debug.LogWarning("Out of range!");
        }
        else
        {
            if (isVectorInXZArea(pos, snapsize / 2 + last_pos.x, snapsize + last_pos.x,
                last_pos.z - snapsize / 4, last_pos.z + snapsize / 4))
            {
                last_pos = last_pos + new Vector3(snapsize, 0, 0);
                return last_pos;
            }
            else if (isVectorInXZArea(pos, last_pos.x - snapsize, last_pos.x - snapsize / 2,
                last_pos.z - snapsize / 4, last_pos.z + snapsize / 4))
            {
                last_pos = last_pos - new Vector3(snapsize, 0, 0);
                return last_pos;
            }
            else if (isVectorInXZArea(pos, last_pos.x - snapsize / 4, last_pos.x + snapsize / 4,
                last_pos.z + snapsize / 2, last_pos.z + snapsize))
            {
                last_pos = last_pos + new Vector3(0, 0, snapsize);
                return last_pos;
            }
            else if (isVectorInXZArea(pos, last_pos.x - snapsize / 4, last_pos.x + snapsize / 4,
                last_pos.z - snapsize, last_pos.z - snapsize / 2))
            {
                last_pos = last_pos - new Vector3(0, 0, snapsize);
                return last_pos;
            }
            else if (isVectorInXZArea(pos, last_pos.x + snapsize / 2, last_pos.x + snapsize,
                last_pos.z + snapsize / 2, last_pos.z + snapsize))
            {
                last_pos = last_pos + new Vector3(snapsize, 0, snapsize);
                return last_pos;
            }
            else if (isVectorInXZArea(pos, last_pos.x - snapsize, last_pos.x - snapsize / 2,
                last_pos.z + snapsize / 2, last_pos.z + snapsize))
            {
                last_pos = last_pos + new Vector3(-snapsize, 0, snapsize);
                return last_pos;
            }
            else if (isVectorInXZArea(pos, last_pos.x + snapsize / 2, last_pos.x + snapsize,
                last_pos.z - snapsize, last_pos.z - snapsize / 2))
            {
                last_pos = last_pos + new Vector3(snapsize, 0, -snapsize);
                return last_pos;
            }
            else if (isVectorInXZArea(pos, last_pos.x - snapsize, last_pos.x - snapsize / 2,
                last_pos.z - snapsize, last_pos.z - snapsize / 2))
            {
                last_pos = last_pos - new Vector3(snapsize, 0, snapsize);
                return last_pos;
            }
        }

        return last_pos;
    }

    bool CheckAppendVaild(Vector3 lastPoint, Vector3 currentPoint, Vector3 addPoint)
    {
        var dir = currentPoint - lastPoint;
        var dirAppend = addPoint - currentPoint;

        switch (currentRoadLane)
        {
            case ROADLANE.RL1:
                return (Vector3.Angle(dir, dirAppend) <= 90);
            case ROADLANE.RL2:
                return (Vector3.Angle(dir, dirAppend) <= 45);
            default:
                return true;
        }
    }

    bool isVectorInXZArea(Vector3 pos, float x_from, float x_to, float z_from, float z_to)
    {
        var cond_1 = x_from <= pos.x && pos.x <= x_to;
        var cond_2 = z_from <= pos.z && pos.z <= z_to;

        return cond_1 && cond_2;
    }

    // Spawn SplineComputer and Apply to spline_computer variable.
    void SpawnPath()
    {
        UnityEngine.Debug.LogWarning("Spawn Path!");
        if (current_spline)
        {
            current_spline = null;
            new_index = 0;
        }

        switch (currentRoadLane)
        {
            case ROADLANE.RL1:
                SplinePrefab = roadPrefabs[0];
                break;
            case ROADLANE.RL2:
                SplinePrefab = roadPrefabs[1];
                break;
            case ROADLANE.RL3:
                SplinePrefab = roadPrefabs[2];
                break;
            case ROADLANE.RL4:
                SplinePrefab = roadPrefabs[3];
                break;
        }

        current_spline = Instantiate(SplinePrefab, pos, Quaternion.identity);

        meshReform(current_spline);
    }

    // Spawn SplineComputer independently.
    SplineComputer InsPath(Vector3 pos)
    {
        switch (currentRoadLane)
        {
            case ROADLANE.RL1:
                SplinePrefab = roadPrefabs[0];
                break;
            case ROADLANE.RL2:
                SplinePrefab = roadPrefabs[1];
                break;
            case ROADLANE.RL3:
                SplinePrefab = roadPrefabs[2];
                break;
            case ROADLANE.RL4:
                SplinePrefab = roadPrefabs[3];
                break;
        }

        var spline = Instantiate(SplinePrefab, pos, Quaternion.identity);

        meshReform(spline);

        return spline;
    }

    void meshReform(SplineComputer spline)
    {
        switch (currentRoadLane)
        {
            case ROADLANE.RL1:
                for (var i = 0; i < 6; i++)
                {
                    spline.GetComponent<SplineMesh>().meshReduce(i, 1);
                }

                break;
            case ROADLANE.RL2:
                spline.GetComponent<SplineMesh>().meshReduce(6, 4);
                spline.GetComponent<SplineMesh>().meshReduce(7, 4);
                break;
        }
    }

    // Append path when snapping event on. Return true when snapping event on.
    bool AppendPath()
    {
        if (last_x != SnapToGridPoint(pos, snapsize).x || last_z != SnapToGridPoint(pos, snapsize).z)
        {
            var x = SnapToGridPoint(pos, snapsize).x;
            var z = SnapToGridPoint(pos, snapsize).z;

            var last_index = current_spline.GetPoints().Length - 1;

            var cond = CheckAppendVaild(
                current_spline.GetPoint(last_index - 1).position,
                current_spline.GetPoint(last_index).position,
                new Vector3(x, 0, z));

            if (cond || current_spline.GetPoints().Length == 1)
            {
                current_spline.SetPointNormal(new_index, def_normal);
                current_spline.SetPointSize(new_index, 1);
                current_spline.SetPointPosition(new_index, new Vector3(x, def_y, z));

                last_x = x;
                last_z = z;

                return true;
            }
            else
            {
                UnityEngine.Debug.LogWarning("Point is out of range!");
            }
        }

        return false;
    }

    // Return true when snapping event on.
    // Same feature with AppendPath()
    public bool CheckSnap()
    {
        if (last_x != SnapToGridPoint(pos, snapsize).x || last_z != SnapToGridPoint(pos, snapsize).z)
        {
            last_x = SnapToGridPoint(pos, snapsize).x;
            last_z = SnapToGridPoint(pos, snapsize).z;

            return true;
        }

        return false;
    }

    // WARNING - To make this function work, I changed below thing.
    // CHANGED - Changed `spline` variable in SplineComputer to public (from private)
    // Body of this function referred DeletePointModule.cs
    void RemovePoint(int index)
    {
        var p = current_spline.spline.points;

        if (index < p.Length && index >= 0)
        {
            ArrayUtility.RemoveAt(ref p, index);
            current_spline.spline.points = p;
        }
        else
        {
            UnityEngine.Debug.LogError("Out of Index! (RemovePoint)");
        }

        if (current_spline)
        {
            current_spline.Rebuild(true);
        }
    }

    // Remove point with point ref.
    void RemovePoint(SplinePoint point)
    {
        var p = current_spline.spline.points;

        ArrayUtility.RemoveAt(ref p, ArrayUtility.IndexOf(p, point));
        current_spline.spline.points = p;

        if (current_spline)
        {
            current_spline.Rebuild(true);
        }
    }

    // Simply Ray-trace and Set mouse position.
    void RayTrace()
    {
        var ray = cm.ScreenPointToRay(Input.mousePosition);
        RaycastHit hitData;

        if (Physics.Raycast(ray, out hitData, 10000))
        {
            pos = hitData.point;
        }
    }

    // Spawn SplineComputer and Change mode to Append.
    void runBuildMode()
    {
        UnityEngine.Debug.LogWarning("RunBuild!");

        new_index = 0;

        SpawnPath();

        if (current_spline)
        {
            current_spline.Rebuild(true);
        }

        AppendPath();
        new_index++;

        current_mode = MODE.APPEND;
    }

    // Append Point when snapping event on. Also Handle Cleaning Joined Path.
    void runAppendModeGrid()
    {
        if (Input.GetMouseButton(0))
        {
            // Head Join.
            if (joinmode == JOINMODE.HEAD)
            {
                if (CheckSnap())
                {
                    // HEAD APPEND CODE
                    var cond = CheckAppendVaild(
                        selected_spline.GetPoint(1).position,
                        selected_spline.GetPoint(0).position,
                        snap_pos);

                    if (cond || current_spline.GetPoints().Length == 1)
                    {
                        var points = selected_spline.GetPoints();

                        for (var i = 0; i < points.Length; i++)
                        {
                            selected_spline.SetPoint(i + 1, points[i]);
                        }

                        selected_spline.SetPointNormal(0, def_normal);
                        selected_spline.SetPointSize(new_index, 1);
                        selected_spline.SetPointPosition(0, snap_pos);

                        // CHECK JOIN DURING APPEND (HEAD)
                        SplineComputer check_spline = null;
                        foreach (var spline in GetSplineComputers(snap_pos))
                        {
                            if (spline != selected_spline)
                            {
                                check_spline = spline;
                            }
                        }

                        if (check_spline != null && check_spline != selected_spline)
                        {
                            if ((check_spline.GetPoints().First().position == snap_pos ||
                                 check_spline.GetPoints().Last().position == snap_pos))
                            {
                                UnityEngine.Debug.LogWarning("Join 2-crossroad (HEAD)");
                                
                                var haveSameCrossroad = GetRefCrossroads(selected_spline)
                                    .Any(cros => GetRefCrossroads(check_spline).Contains(cros));

                                if (haveSameCrossroad)
                                {
                                    // CROSSROAD LOOP (HEAD APPEND)
                                    UnityEngine.Debug.LogWarning("LOOP");
                                    var spline = MergeSplines(check_spline, selected_spline);
                                    spline.isLoop = true;
                                }
                                else
                                {
                                    MergeSplines(check_spline, selected_spline);
                                }
                            }
                            else
                            {
                                UnityEngine.Debug.LogWarning("Join 3-crossroad (HEAD)");

                                var index = getSplinePointIndex(check_spline, getSplinePoint(snap_pos, check_spline));

                                var new_spline = SplitSpline(index, check_spline);

                                var crossroad = new Crossroad();
                                crossroad.AddRoad(check_spline);
                                crossroad.AddRoad(new_spline);
                                crossroad.AddRoad(selected_spline);
                                crossroad.SetPosition(new_spline.GetPoint(0).position);

                                crossroads.Add(crossroad);
                            }
                        }

                        // Check Appending Spline is Closed.
                        if (check_spline == null && selected_spline.GetPoints().First().position ==
                            selected_spline.GetPoints().Last().position)
                        {
                            UnityEngine.Debug.LogWarning("LOOP");
                            selected_spline.Close();
                        }
                    }
                }
            }
            else
            {
                if (AppendPath())
                {
                    // CHECK JOIN DURING APPEND (TAIL)
                    SplineComputer check_spline = null;

                    foreach (var spline in GetSplineComputers(snap_pos))
                    {
                        if (spline != current_spline)
                        {
                            check_spline = spline;
                        }
                    }

                    if (check_spline != null && check_spline != current_spline)
                    {
                        if ((check_spline.GetPoints().First().position == snap_pos ||
                             check_spline.GetPoints().Last().position == snap_pos))
                        {
                            UnityEngine.Debug.LogWarning("Join 2-roads (APPEND)");

                            var haveSameCrossroad = GetRefCrossroads(current_spline)
                                .Any(cros => GetRefCrossroads(check_spline).Contains(cros));

                            if (haveSameCrossroad)
                            {
                                // CROSSROAD LOOP (TAIL APPEND)
                                var spline = MergeSplines(check_spline, current_spline);
                                spline.isLoop = true;
                            }
                            else
                            {
                                var spline = MergeSplines(check_spline, current_spline);
                            }
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning("Join 3-crossroad (APPEND)");

                            var points = check_spline.GetPoints();
                            var index = 0;

                            for (var i = 0; i < points.Length; i++)
                            {
                                if (points[i].position == snap_pos)
                                {
                                    index = i;
                                }
                            }

                            var new_spline = SplitSpline(index, check_spline);

                            var crossroad = new Crossroad();
                            crossroad.AddRoad(new_spline);
                            crossroad.AddRoad(check_spline);
                            crossroad.AddRoad(current_spline);
                            crossroad.SetPosition(new_spline.GetPoint(0).position);

                            crossroads.Add(crossroad);
                        }
                    }

                    // Check Appending Spline is Closed.
                    if (check_spline == null && current_spline.GetPoints().First().position ==
                        current_spline.GetPoints().Last().position)
                    {
                        UnityEngine.Debug.LogWarning("LOOP");
                        current_spline.Close();
                    }

                    // CHECK JOIN DURING BUILD
                    if (joinmode != JOINMODE.NONE)
                    {
                        if (joinmode == JOINMODE.TO3)
                        {
                            UnityEngine.Debug.LogWarning("Join 3-crossroad (BUILD)");

                            // Check If selected spline referenced by another crossroad
                            var refCrossroads = GetRefCrossroads(selected_spline);

                            if (refCrossroads.Count != 0)
                            {
                                var checkLastPos = selected_spline.GetPoints().Last().position;

                                cross_new_spline = SplitSpline(selected_index, selected_spline);
                                cross_old_spline = selected_spline;

                                foreach (var refCros in refCrossroads)
                                {
                                    if (refCros.getPosition() == checkLastPos)
                                    {
                                        refCros.RemoveRoad(selected_spline);
                                        refCros.AddRoad(cross_new_spline);
                                    }
                                }

                                var crossroad = new Crossroad();
                                crossroad.AddRoad(cross_new_spline);
                                crossroad.AddRoad(cross_old_spline);
                                crossroad.AddRoad(current_spline);

                                crossroad.SetPosition(cross_new_spline.GetPoint(0).position);

                                crossroads.Add(crossroad);
                            }
                            else
                            {
                                cross_new_spline = SplitSpline(selected_index, selected_spline);
                                cross_old_spline = selected_spline;

                                var crossroad = new Crossroad();
                                crossroad.AddRoad(cross_new_spline);
                                crossroad.AddRoad(cross_old_spline);
                                crossroad.AddRoad(current_spline);
                                crossroad.SetPosition(cross_new_spline.GetPoint(0).position);

                                crossroads.Add(crossroad);
                            }

                            joinmode = JOINMODE.NONE;
                            selected_spline = null;
                            new_index++;
                        }
                        else if (joinmode == JOINMODE.TO4)
                        {
                            UnityEngine.Debug.LogWarning("Join 4-crossroad (BUILD)");

                            selected_crossroad.AddRoad(current_spline);

                            new_index++;
                            joinmode = JOINMODE.NONE;
                        }
                        else if (joinmode == JOINMODE.TO3_SPLIT)
                        {
                            var crossroad = new Crossroad();
                            crossroad.SetRoads(selectedSplines);
                            crossroad.AddRoad(current_spline);
                            crossroad.SetPosition(current_spline.GetPoint(0).position);

                            crossroads.Add(crossroad);

                            joinmode = JOINMODE.NONE;
                            selectedSplines = null;
                            new_index++;
                        }
                    }
                    else
                    {
                        new_index++;
                    }
                }
            }

            if (current_spline)
            {
                current_spline.Rebuild(true);
            }

            if (cross_new_spline)
            {
                cross_new_spline.Rebuild(true);
            }

            if (cross_old_spline)
            {
                cross_old_spline.Rebuild(true);
            }

            if (selected_spline)
            {
                selected_spline.Rebuild(true);
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            current_spline = null;
            cross_old_spline = null;
            cross_new_spline = null;

            selected_spline = null;
            selected_index = 0;
            selected_crossroad = null;

            new_index = 0;
            last_x = 0;
            last_z = 0;

            joinmode = JOINMODE.NONE;
            current_mode = MODE.BUILD;
        }
        else if (Input.GetMouseButton(1))
        {
            if (Input.GetMouseButtonDown(1))
            {
                UnityEngine.Debug.LogWarning("Undo Last Point Creation");

                RemovePoint(new_index - 1);
                new_index--;
            }
        }
    }

    // Get Point count with position.
    int GetPointIndex(Vector3 pos)
    {
        var points = current_spline.GetPoints();

        for (var i = 0; i < points.Length; i++)
        {
            if (points[i].position == snap_pos)
            {
                return i;
            }
        }

        return -1;
    }

    List<SplineComputer> GetSplineComputers(Vector3 pos)
    {
        var spline_list = GameObject.FindObjectsOfType<SplineComputer>();
        var return_list = new List<SplineComputer>();

        foreach (var spline in spline_list)
        {
            var points = spline.GetPoints();

            for (var i = 0; i < points.Length; i++)
            {
                if (pos == points[i].position)
                {
                    return_list.Add(spline);
                    break;
                }
            }
        }

        return return_list;
    }

    SplinePoint getSplinePoint(Vector3 pos, SplineComputer spline)
    {
        foreach (var point in spline.GetPoints())
        {
            if (point.position == pos)
            {
                return point;
            }
        }

        return new SplinePoint();
    }

    int getSplinePointIndex(SplineComputer spline, SplinePoint point)
    {
        var points = spline.GetPoints();

        for (var i = 0; i < points.Length; i++)
        {
            if (points[i].position == point.position)
            {
                return i;
            }
        }

        return -1;
    }

    // Split Spline and return newly spawned SplineComputer.
    SplineComputer SplitSpline(int index, SplineComputer spline, bool reverse = false)
    {
        var oldPoints = new List<SplinePoint>();
        var newPoints = new List<SplinePoint>();

        var originPoints = spline.GetPoints();

        if (!reverse)
        {
            for (var j = 0; j <= index; j++)
            {
                oldPoints.Add(originPoints[j]);
            }

            for (var j = index; j < originPoints.Length; j++)
            {
                newPoints.Add(originPoints[j]);
            }
        }
        else
        {
            for (var i = 0; i <= index; i++)
            {
                newPoints.Add(originPoints[i]);
            }

            for (var i = index; i < originPoints.Length; i++)
            {
                oldPoints.Add(originPoints[i]);
            }
        }

        spline.SetPoints(oldPoints.ToArray());

        var newSpline = InsPath(newPoints[0].position);
        newSpline.SetPoints(newPoints.ToArray());

        if (spline.isLoop)
        {
            debugPoint(GetRefCrossroads(spline).First().getPosition());
            foreach (var cros in GetRefCrossroads(spline))
            {
                newSpline.isLoop = false;
                cros.AddRoad(newSpline);
            }
            spline.isLoop = false;
        }
        
        spline.Rebuild(true);
        newSpline.Rebuild(true);

        return newSpline;
    }

    // Check two vector create Clockwise or Counterclockwise.
    bool isVectorGoClockwise(Vector3 from, Vector3 to)
    {
        if (Vector3.SignedAngle(from, to, new Vector3(0, 1, 0)) <= 0)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    // Check two vector is Parallel.
    bool isVectorParallel(Vector3 v1, Vector3 v2)
    {
        if (Vector3.Angle(v1, v2) == 0 || Vector3.Angle(v1, v2) == 180)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    bool isVectorVertical(Vector3 v1, Vector3 v2)
    {
        if (Vector3.Dot(v1, v2) == 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    SplineComputer MergeSplines(SplineComputer s1, SplineComputer s2)
    {
        var mergemode = MERGEMODE.NONE;

        if (s1.GetPoints().Last().position == s2.GetPoints().First().position &&
            s1.GetPoints().First().position == s2.GetPoints().Last().position) mergemode = MERGEMODE.LOOP;
        else if (s1.GetPoints().Last().position == s2.GetPoints().Last().position) mergemode = MERGEMODE.LL;
        else if (s1.GetPoints().First().position == s2.GetPoints().First().position) mergemode = MERGEMODE.FF;
        else if (s1.GetPoints().Last().position == s2.GetPoints().First().position) mergemode = MERGEMODE.LF;
        else if (s1.GetPoints().First().position == s2.GetPoints().Last().position) mergemode = MERGEMODE.FL;

        if (mergemode == MERGEMODE.LL)
        {
            UnityEngine.Debug.LogWarning("LL");
            var index = s1.GetPoints().Length;

            var points = s2.GetPoints();
            for (var i = points.Length - 2; i >= 0; i--)
            {
                s1.SetPoint(index, points[i]);
                index++;
            }

            var refCrossroad = crossroads.FirstOrDefault(cros => cros.getRoads().Contains(s2));
            if (refCrossroad != null)
            {
                refCrossroad.RemoveRoad(s2);
                refCrossroad.AddRoad(s1);
            }

            Destroy(s2.gameObject);
            return s1;
        }
        else if (mergemode == MERGEMODE.FF)
        {
            UnityEngine.Debug.LogWarning("FF");
            var points = s1.GetPoints();
            var points2 = s2.GetPoints();

            var index = 0;

            for (var i = 0; i < points.Length; i++)
            {
                s1.SetPoint(i + points2.Length - 1, points[i]);
            }

            for (var i = points2.Length - 1; i >= 1; i--)
            {
                s1.SetPoint(index, points2[i]);
                index++;
            }

            var refCrossroad = crossroads.FirstOrDefault(cros => cros.getRoads().Contains(s2));
            if (refCrossroad != null)
            {
                refCrossroad.RemoveRoad(s2);
                refCrossroad.AddRoad(s1);
            }

            Destroy(s2.gameObject);
            return s1;
        }
        else if (mergemode == MERGEMODE.LF)
        {
            UnityEngine.Debug.LogWarning("LF");
            var index = s1.GetPoints().Length;

            var points = s2.GetPoints();
            for (var i = 1; i < points.Length; i++)
            {
                s1.SetPoint(index, points[i]);
                index++;
            }

            var refCrossroad = crossroads.FirstOrDefault(cros => cros.getRoads().Contains(s2));
            if (refCrossroad != null)
            {
                refCrossroad.RemoveRoad(s2);
                refCrossroad.AddRoad(s1);
            }

            Destroy(s2.gameObject);
            return s1;
        }
        else if (mergemode == MERGEMODE.FL)
        {
            UnityEngine.Debug.LogWarning("FL");
            var index = s2.GetPoints().Length;

            var points = s1.GetPoints();

            for (var i = 1; i <= points.Length - 1; i++)
            {
                s2.SetPoint(index, points[i]);
                index++;
            }

            var refCrossroad = crossroads.FirstOrDefault(cros => cros.getRoads().Contains(s1));
            if (refCrossroad != null)
            {
                refCrossroad.RemoveRoad(s1);
                refCrossroad.AddRoad(s2);
            }

            Destroy(s1.gameObject);
            return s2;
        }
        else if (mergemode == MERGEMODE.LOOP)
        {
            var refCrossroad = crossroads.FirstOrDefault(cros => cros.getRoads().Contains(s1));

            if (s1.GetPoints().Last().position == refCrossroad.getPosition())
            {
                // FL
                UnityEngine.Debug.LogWarning("LOOP - FL");
                var index = s2.GetPoints().Length;

                var points = s1.GetPoints();

                for (var i = 1; i <= points.Length - 1; i++)
                {
                    s2.SetPoint(index, points[i]);
                    index++;
                }
                
                if (refCrossroad != null)
                {
                    refCrossroad.RemoveRoad(s1);
                    refCrossroad.AddRoad(s2);
                }

                Destroy(s1.gameObject);
                return s2;
            }
            else if (s1.GetPoint(0).position == refCrossroad.getPosition())
            {
                // LF
                UnityEngine.Debug.LogWarning("LOOP - LF");
                var index = s1.GetPoints().Length;

                var points = s2.GetPoints();
                for (var i = 1; i < points.Length; i++)
                {
                    s1.SetPoint(index, points[i]);
                    index++;
                }
                
                if (refCrossroad != null)
                {
                    refCrossroad.RemoveRoad(s2);
                    refCrossroad.AddRoad(s1);
                }

                Destroy(s2.gameObject);
                return s1;
            }
        }
        else if (mergemode == MERGEMODE.NONE)
        {
            UnityEngine.Debug.LogWarning("NONE");
            return null;
        }

        return null;
    }

    Vector3 GetSplinePosition(SplineComputer spline)
    {
        return spline.GetPoint(spline.GetPoints().Length / 2).position;
        // return (spline.GetPoints().First().position + spline.GetPoints().Last().position) / 2;
    }

    List<Crossroad> GetRefCrossroads(SplineComputer spline)
    {
        return crossroads.Where(cros => cros.getRoads().Contains(spline)).ToList();
    }

    void Start()
    {
        cm = GetComponentInChildren<Camera>();
    }

    // TODO - LOOP Spline Append Spliting Code (BUILD, TAIL APPEND, HEAD APPEND / contain CROSSROAD or not contain)

    void Update()
    {
        RayTrace();

        snap_pos = new Vector3(SnapGrid(pos.x, snapsize), 0, SnapGrid(pos.z, snapsize));
        last_pos = snap_pos;
        debugobj.GetComponent<Transform>().position = snap_pos;

        // Crossroad Clean Line Code
        for (var index = 0; index < crossroads.Count; index++)
        {
            var cros = crossroads[index];

            LogTextOnPos(index + "C ", cros.getPosition()); // DEBUG
            
            var roads = new List<SplineComputer>(cros.getRoads()); // COPY LIST
            var dirList = new List<Vector3>();
            var loopedRoad = roads.FirstOrDefault(road => road.isLoop);

            if (loopedRoad != null)
            {
                UnityEngine.Debug.LogWarning("Count : " + roads.Count(road => road.isLoop));
                
                roads.Add(loopedRoad);
                roads = (from road in roads orderby road.isLoop ascending select road).ToList();

                var loopRoadStartIndex = 0;
                for (var i = 0; i < roads.Count; i++)
                {
                    if (!roads[i].isLoop) continue;
                    loopRoadStartIndex = i;
                    break;
                }

                // Make dirList
                for (var i = 0; i < roads.Count; i++)
                {
                    if (roads[i].isLoop) continue;
                    LogTextOnPos(index + "C - SP - " + i, GetSplinePosition(roads[i]));

                    if (roads[i].GetPoints().Last().position == cros.getPosition())
                    {
                        var last_index = roads[i].GetPoints().Length - 1;

                        var dir = roads[i].GetPoint(last_index - 1).position - cros.getPosition();
                        dirList.Add(dir);
                    }
                    else if (roads[i].GetPoints().First().position == cros.getPosition())
                    {
                        var dir = roads[i].GetPoint(1).position - cros.getPosition();
                        dirList.Add(dir);
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("ERROR!");
                    }
                }
                
                LogTextOnPos(index + "C - LSP ", GetSplinePosition(loopedRoad));

                var joinIndex = 0;
                for (var i = 0; i < loopedRoad.GetPoints().Length; i++)
                {
                    if (loopedRoad.GetPoint(i).position != cros.getPosition()) continue;
                    joinIndex = i;
                    break;
                }

                var loopForwardDir = loopedRoad.GetPoint(joinIndex + 1).position -
                                     loopedRoad.GetPoint(joinIndex).position;

                var backIndex = joinIndex - 1;
                if (backIndex < 0)
                    backIndex = loopedRoad.GetPoints().Length - 1 + backIndex;

                var loopBackwardDir = loopedRoad.GetPoint(backIndex).position -
                                      loopedRoad.GetPoint(joinIndex).position;

                dirList.Add(loopForwardDir);
                dirList.Add(loopBackwardDir);

                // Apply To SplineMesh
                for (var i = 0; i < roads.Count; i++)
                {
                    var isRight = false;
                    var isLeft = false;

                    // Straigt Road
                    if (!roads[i].isLoop)
                    {
                        // Last Point
                        if (roads[i].GetPoints().Last().position == cros.getPosition())
                        {
                            foreach (var dir in dirList)
                            {
                                if (isVectorVertical(dirList[i], dir))
                                {
                                    if (isVectorGoClockwise(dirList[i], dir))
                                    {
                                        isLeft = true;
                                    }
                                    else
                                    {
                                        isRight = true;
                                    }
                                }
                            }

                            var per = roads[i].Project(cros.getPosition() + dirList[i] / divider).percent;

                            roads[i].GetComponent<SplineMesh>().GetChannel(1).clipTo = per;

                            if (isLeft && !isRight)
                            {
                                roads[i].GetComponent<SplineMesh>().GetChannel(3).clipTo = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(5).clipTo = per;
                            }
                            else if (isRight && !isLeft)
                            {
                                roads[i].GetComponent<SplineMesh>().GetChannel(2).clipTo = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(4).clipTo = per;
                            }
                            else if (isLeft && isRight)
                            {
                                roads[i].GetComponent<SplineMesh>().GetChannel(3).clipTo = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(5).clipTo = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(2).clipTo = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(4).clipTo = per;
                            }
                        }
                        // First Point
                        else if (roads[i].GetPoint(0).position == cros.getPosition())
                        {
                            foreach (var dir in dirList)
                            {
                                if (isVectorVertical(dirList[i], dir))
                                {
                                    if (isVectorGoClockwise(dirList[i], dir))
                                    {
                                        isRight = true;
                                    }
                                    else
                                    {
                                        isLeft = true;
                                    }
                                }
                            }

                            var per = roads[i].Project(cros.getPosition() + dirList[i] / divider).percent;

                            roads[i].GetComponent<SplineMesh>().GetChannel(1).clipFrom = per;

                            if (isLeft && !isRight)
                            {
                                roads[i].GetComponent<SplineMesh>().GetChannel(3).clipFrom = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(5).clipFrom = per;
                            }
                            else if (isRight && !isLeft)
                            {
                                roads[i].GetComponent<SplineMesh>().GetChannel(2).clipFrom = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(4).clipFrom = per;
                            }
                            else if (isLeft && isRight)
                            {
                                roads[i].GetComponent<SplineMesh>().GetChannel(3).clipFrom = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(5).clipFrom = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(2).clipFrom = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(4).clipFrom = per;
                            }
                        }
                    }
                    // Looped Roads
                    else
                    {
                        foreach (var dir in dirList)
                        {
                            if (!isVectorParallel(dirList[i], dir))
                            {
                                if (isVectorGoClockwise(dirList[i], dir))
                                {
                                    if (i == loopRoadStartIndex)
                                        isRight = true;
                                    else if (i == loopRoadStartIndex + 1)
                                        isLeft = true;
                                }
                                else
                                {
                                    if (i == loopRoadStartIndex)
                                        isLeft = true;
                                    else if (i == loopRoadStartIndex + 1)
                                        isRight = true;
                                }
                            }
                        }

                        var per = roads[i].Project(cros.getPosition() + dirList[i] / divider).percent;

                        if (i == loopRoadStartIndex)
                        {
                            roads[i].GetComponent<SplineMesh>().GetChannel(1).clipFrom = per;

                            if (isLeft && !isRight)
                            {
                                roads[i].GetComponent<SplineMesh>().GetChannel(3).clipFrom = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(5).clipFrom = per;
                            }
                            else if (isRight && !isLeft)
                            {
                                roads[i].GetComponent<SplineMesh>().GetChannel(2).clipFrom = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(4).clipFrom = per;
                            }
                            else if (isLeft && isRight)
                            {
                                roads[i].GetComponent<SplineMesh>().GetChannel(3).clipFrom = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(5).clipFrom = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(2).clipFrom = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(4).clipFrom = per;
                            }
                        }
                        else if (i == loopRoadStartIndex + 1)
                        {
                            roads[i].GetComponent<SplineMesh>().GetChannel(1).clipTo = per;

                            if (isLeft && !isRight)
                            {
                                roads[i].GetComponent<SplineMesh>().GetChannel(3).clipTo = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(5).clipTo = per;
                            }
                            else if (isRight && !isLeft)
                            {
                                roads[i].GetComponent<SplineMesh>().GetChannel(2).clipTo = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(4).clipTo = per;
                            }
                            else if (isLeft && isRight)
                            {
                                roads[i].GetComponent<SplineMesh>().GetChannel(3).clipTo = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(5).clipTo = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(2).clipTo = per;
                                roads[i].GetComponent<SplineMesh>().GetChannel(4).clipTo = per;
                            }
                        }
                    }
                }
            }
            else
            {
                for (var i = 0; i < roads.Count; i++)
                {
                    LogTextOnPos(index + "C - SP - " + i, GetSplinePosition(roads[i]));

                    if (roads[i].GetPoints().Last().position == cros.getPosition())
                    {
                        var last_index = roads[i].GetPoints().Length - 1;

                        var dir = roads[i].GetPoint(last_index - 1).position - cros.getPosition();
                        dirList.Add(dir);
                    }
                    else if (roads[i].GetPoints().First().position == cros.getPosition())
                    {
                        var dir = roads[i].GetPoint(1).position - cros.getPosition();
                        dirList.Add(dir);
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("ERROR!");
                    }
                }

                for (var i = 0; i < roads.Count; i++)
                {
                    var isRight = false;
                    var isLeft = false;

                    if (roads[i].GetPoints().Last().position == cros.getPosition())
                    {
                        foreach (var dir in dirList)
                        {
                            if (isVectorVertical(dirList[i], dir))
                            {
                                if (isVectorGoClockwise(dirList[i], dir))
                                {
                                    isLeft = true;
                                }
                                else
                                {
                                    isRight = true;
                                }
                            }
                        }

                        var per = roads[i].Project(cros.getPosition() + dirList[i] / divider).percent;

                        roads[i].GetComponent<SplineMesh>().GetChannel(1).clipTo = per;

                        if (isLeft && !isRight)
                        {
                            roads[i].GetComponent<SplineMesh>().GetChannel(3).clipTo = per;
                            roads[i].GetComponent<SplineMesh>().GetChannel(5).clipTo = per;
                        }
                        else if (isRight && !isLeft)
                        {
                            roads[i].GetComponent<SplineMesh>().GetChannel(2).clipTo = per;
                            roads[i].GetComponent<SplineMesh>().GetChannel(4).clipTo = per;
                        }
                        else if (isLeft && isRight)
                        {
                            roads[i].GetComponent<SplineMesh>().GetChannel(3).clipTo = per;
                            roads[i].GetComponent<SplineMesh>().GetChannel(5).clipTo = per;
                            roads[i].GetComponent<SplineMesh>().GetChannel(2).clipTo = per;
                            roads[i].GetComponent<SplineMesh>().GetChannel(4).clipTo = per;
                        }
                    }
                    else if (roads[i].GetPoints().First().position == cros.getPosition())
                    {
                        foreach (var dir in dirList)
                        {
                            if (isVectorVertical(dirList[i], dir))
                            {
                                if (isVectorGoClockwise(dirList[i], dir))
                                {
                                    isRight = true;
                                }
                                else
                                {
                                    isLeft = true;
                                }
                            }
                        }

                        var per = roads[i].Project(cros.getPosition() + dirList[i] / divider).percent;

                        roads[i].GetComponent<SplineMesh>().GetChannel(1).clipFrom = per;

                        if (isLeft && !isRight)
                        {
                            roads[i].GetComponent<SplineMesh>().GetChannel(3).clipFrom = per;
                            roads[i].GetComponent<SplineMesh>().GetChannel(5).clipFrom = per;
                        }
                        else if (isRight && !isLeft)
                        {
                            roads[i].GetComponent<SplineMesh>().GetChannel(2).clipFrom = per;
                            roads[i].GetComponent<SplineMesh>().GetChannel(4).clipFrom = per;
                        }
                        else if (isLeft && isRight)
                        {
                            roads[i].GetComponent<SplineMesh>().GetChannel(3).clipFrom = per;
                            roads[i].GetComponent<SplineMesh>().GetChannel(5).clipFrom = per;
                            roads[i].GetComponent<SplineMesh>().GetChannel(2).clipFrom = per;
                            roads[i].GetComponent<SplineMesh>().GetChannel(4).clipFrom = per;
                        }
                    }
                }
            }
        }

        // Change MODE
        if (Input.GetKeyDown(KeyCode.B))
        {
            UnityEngine.Debug.LogWarning("Build Mode Enabled!");
            current_mode = MODE.BUILD;
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            UnityEngine.Debug.LogWarning("Remove Mode Enabled!");
            current_mode = MODE.REMOVE;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            currentRoadLane = ROADLANE.RL1;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            currentRoadLane = ROADLANE.RL2;
        }

        if (current_mode == MODE.BUILD)
        {
            // BUILD MODE
            if (Input.GetMouseButtonDown(0))
            {
                var spline_list = GameObject.FindObjectsOfType<SplineComputer>();

                Crossroad crossroad = null;

                foreach (var cros in crossroads)
                {
                    if (snap_pos == cros.getPosition())
                    {
                        crossroad = cros;
                        break;
                    }
                }

                if (crossroad != null)
                {
                    if (crossroad.getRoads().Count == 3)
                    {
                        joinmode = JOINMODE.TO4;

                        selected_crossroad = crossroad;
                        
                        runBuildMode();
                    }
                }
                else
                {
                    var splines = GetSplineComputers(snap_pos);
                    SplineComputer spline = null;

                    if (splines.Count == 1)
                    {
                        spline = splines[0];

                        var point = getSplinePoint(snap_pos, spline);
                        var point_index = getSplinePointIndex(spline, point);

                        if (point_index != -1)
                        {
                            if (point_index == spline.GetPoints().Count() - 1)
                            {
                                UnityEngine.Debug.LogWarning("Tail Append");

                                new_index = point_index;
                                current_spline = spline;

                                current_mode = MODE.APPEND;
                            }
                            else if (point_index == 0)
                            {
                                UnityEngine.Debug.LogWarning("Head Append");

                                selected_spline = spline;

                                current_mode = MODE.APPEND;
                                joinmode = JOINMODE.HEAD;

                                last_x = snap_pos.x;
                                last_z = snap_pos.z;
                            }
                            else
                            {
                                UnityEngine.Debug.LogWarning("Split for Join");

                                selected_spline = spline;
                                selected_index = point_index;

                                joinmode = JOINMODE.TO3;

                                runBuildMode();
                            }
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning("ERROR - Can't find Point in Spline");
                        }
                    }
                    else if (splines.Count == 0)
                    {
                        runBuildMode();
                    }
                    else if (splines.Count == 2)
                    {
                        // Not Crossroad, But Splines are splitted.
                        UnityEngine.Debug.LogWarning("SPLIT TO3");

                        selectedSplines = splines;

                        current_mode = MODE.APPEND;
                        joinmode = JOINMODE.TO3_SPLIT;

                        runBuildMode();
                    }
                }
            }
        }
        else if (current_mode == MODE.APPEND)
        {
            // APPEND MODE
            runAppendModeGrid();
        }
        else if (current_mode == MODE.REMOVE)
        {
            // REMOVE MODE
            if (Input.GetMouseButton(0))
            {
                var index = GetPointIndex(snap_pos);
                RemovePoint(index - 1);
            }

            // TODO - Remove points in the middle of spline
            // Now We can use checking mouse position value function.
        }
    }
}