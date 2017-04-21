using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GraphLead : MonoBehaviour {

	TextMeshPro tYmin, tYmax, tXmin, tXmax;
	GameObject pymax, pymin, pxmax;
	public int maxPoints = 100;
	//logical min/max
	Vector2 lMin = new Vector2();
	Vector2 lMax = new Vector2();
	//physical min/max
	Vector2 pMin = new Vector2();
	Vector2 pMax = new Vector2();

 //convert logical coord to physical coord
	public Vector3 Logical2PhysicalCoord(float x, float y)
	{
		return L2PC_RelX(x - lMin.x, y);
	}
 	public Vector3 L2PC_RelX(float x, float y)
	{
		var ret = new Vector3();
		//normalized logical coord (0-1) * physical range + origin offset
		ret.x = (x) / (lMax.x - lMin.x) * (pMax.x - pMin.x) + pMin.x;
		ret.y = (y - lMin.y) / (lMax.y - lMin.y) * (pMax.y - pMin.y) + pMin.y;
		ret.z = pymin.transform.position.z;
		return ret;
	}
	public void SetMaxY(float y)
	{
		Debug.Log("old maxy: " + lMax.y + " new: " + y);
		lMax.y = y;
	}
	public void ResetYBounds()
	{
		if (firstThisUpdate)
		{
			firstThisUpdate = false;
			lMax = Vector2.zero;
			lMin = Vector2.zero;
		}
	}
	public void UpdateLBounds(Vector2 _lMin, Vector2 _lMax)
	{
		lMin = Vector2.Min(lMin, _lMin);
		lMin.x = _lMin.x;
		lMax = Vector2.Max(lMax, _lMax);
	}
	// Use this for initialization
	void Start () {
		
		//axis labels (text)
		tYmin = transform.FindChild("ymin").GetComponent<TextMeshPro>();
		tXmin = transform.FindChild("xmin").GetComponent<TextMeshPro>();
		tYmax = transform.FindChild("ymax").GetComponent<TextMeshPro>();
		tXmax = transform.FindChild("xmax").GetComponent<TextMeshPro>();

		//physical bounds of graph
		pymin = transform.FindChild("pymin").gameObject;
		pymax = transform.FindChild("pymax").gameObject;
		pxmax = transform.FindChild("pxmax").gameObject;

		//logical bounds, aggregate of all sub graphs
        lMin = Vector2.zero;
		lMax = new Vector2(maxPoints, 10);
		pMin = new Vector2(pymin.transform.position.x, pymin.transform.position.y);
		pMax = new Vector2(pxmax.transform.position.x, pymax.transform.position.y);

		//set axis
		var axis = transform.FindChild("plane").GetComponent<LineRenderer>();
		axis.SetVertexCount(3);
		axis.SetPosition(0, pymax.transform.position);
		axis.SetPosition(1, pymin.transform.position);
		axis.SetPosition(2, pxmax.transform.position);


	}
	bool firstThisUpdate = true;
	// Update is called once per frame
	void LateUpdate () {
		firstThisUpdate = true;
		tXmin.SetText(lMin.x.ToString("G"));
		tXmax.SetText(lMax.x.ToString("G"));
		tYmin.SetText(lMin.y.ToString("G"));
		tYmax.SetText(lMax.y.ToString("G"));
    }
}
