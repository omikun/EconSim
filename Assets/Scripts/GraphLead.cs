using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GraphLead : MonoBehaviour {

	Text tYmin, tYmax, tXmin, tXmax;
	GameObject pymax, pymin, pxmax;
	public int maxPoints = 100;
	//logical min/max
	Vector2 lMin = new Vector2();
	Vector2 lMax = new Vector2();
	//physical min/max
	Vector2 pMin = new Vector2();
	Vector2 pMax = new Vector2();
	LineRenderer axis;

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
		tYmin = transform.Find("ymin").GetComponent<Text>();
		tXmin = transform.Find("xmin").GetComponent<Text>();
		tYmax = transform.Find("ymax").GetComponent<Text>();
		tXmax = transform.Find("xmax").GetComponent<Text>();

		//physical bounds of graph
		pymin = transform.Find("pymin").gameObject;
		pymax = transform.Find("pymax").gameObject;
		pxmax = transform.Find("pxmax").gameObject;

		//logical bounds, aggregate of all sub graphs
        lMin = Vector2.zero;
		lMax = new Vector2(maxPoints, 10);
		pMin = new Vector2(pymin.transform.position.x, pymin.transform.position.y);
		pMax = new Vector2(pxmax.transform.position.x, pymax.transform.position.y);

		//set axis
		GameObject go = transform.Find("plane").gameObject;
		axis = go.GetComponent<LineRenderer>();
		axis.positionCount = 4;

		var hi = Hierarchy(go);
		Debug.Log(hi + " numPos: " + axis.positionCount );
		axis.SetPosition(0, pymax.transform.position);
		axis.SetPosition(1, pymin.transform.position);
		axis.SetPosition(2, pymin.transform.position);
		axis.SetPosition(3, pxmax.transform.position);


	}
	bool firstThisUpdate = true;
	// Update is called once per frame
	void LateUpdate () {
		return;
		/*
		firstThisUpdate = true;
		//TODO roll this back: rn getting null ref exception
		//tXmin.text = (lMin.x.ToString("n2"));
		//tXmax.text = (lMax.x.ToString("n2"));
		//tYmin.text = (lMin.y.ToString("n2"));
		//tYmax.text = (lMax.y.ToString("n2"));

		//set zero line (position 2 and 3)
		float lyzero = Mathf.InverseLerp(lMin.y, lMax.y, 0);
		var pyzero = Vector3.Lerp(pymin.transform.position, pymax.transform.position, lyzero);
		//var pyzero = lyzero * (pymax.transform.position) - pymin.transform.position;

		axis.SetPosition(2, pyzero);
		var tmp = pxmax.transform.position;
		tmp.y = pyzero.y;
		var hi = Hierarchy(gameObject);
		axis.positionCount = 4;
		Debug.Log(hi + " numPos: " + axis.positionCount + " pos3: " + tmp.ToString());
		axis.SetPosition(3, tmp);
		*/
    }
	string Hierarchy(GameObject go, int num=0)
	{
		string ret = "";
		num++;
		if (num > 4)
			return ret;
		if (go.transform.parent != null)
			ret += Hierarchy(go.transform.parent.gameObject, num);

        ret += ":" + go.name;
        return ret;
	}
}
