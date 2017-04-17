using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GraphMe : MonoBehaviour {
	public GameObject pymax, pymin, pxmax;
	LineRenderer line;
	List<float> inputs = new List<float>();
	const int maxPoints = 100;
	//logical min/max
	Vector2 lMin = new Vector2();
	Vector2 lMax = new Vector2();
	//physical min/max
	Vector2 pMin = new Vector2();
	Vector2 pMax = new Vector2();
	TextMeshPro tYmin, tYmax, tXmin, tXmax;

	// Use this for initialization
	void Start () {
		line = GetComponent<LineRenderer>();
		lMin = Vector2.zero;
		lMax = new Vector2(maxPoints, 10);
		pMin = new Vector2(pymin.transform.position.x, pymin.transform.position.y);
		pMax = new Vector2(pxmax.transform.position.x, pymax.transform.position.y);
		tYmin = transform.parent.Find("ymin").GetComponent<TextMeshPro>();
		tXmin = transform.parent.Find("xmin").GetComponent<TextMeshPro>();
		tYmax = transform.parent.Find("ymax").GetComponent<TextMeshPro>();
		tXmax = transform.parent.Find("xmax").GetComponent<TextMeshPro>();
	}
	//convert logical coord to physical coord
	Vector3 Logical2PhysicalCoord(float x, float y)
	{
		var ret = new Vector3();
		//normalized logical coord (0-1) * physical range + origin offset
		ret.x = (x - lMin.x) / (lMax.x - lMin.x) * (pMax.x - pMin.x) + pMin.x;
		ret.y = (y - lMin.y) / (lMax.y - lMin.y) * (pMax.y - pMin.y) + pMin.y;
		ret.z = pymin.transform.position.z;
		return ret;
	}
	// Update is called once per frame
	public void Tick (float input) {
		lMax.y = Mathf.Max(lMax.y, input);
		lMin.y = Mathf.Min(lMin.y, input);
		//if graph filled, shift all points back one, update last one
		if (inputs.Count >= maxPoints)
		{
			inputs.RemoveAt(0);
			lMin.x += 1;
            lMax.x += 1;
			for (int i=0; i < inputs.Count; i++)
			{
				line.SetPosition(i, Logical2PhysicalCoord(i+lMin.x, inputs[i]));
			}
		}
		inputs.Add(input);
        int x = inputs.Count-1 + (int)lMin.x;
        line.SetPosition(x, Logical2PhysicalCoord(x, input));
		//set all points after
        for (int i = x + 1; i < maxPoints; i++)
        {
            line.SetPosition(i, Logical2PhysicalCoord(x, input));
        }

		//set text
		tXmin.SetText(lMin.x.ToString("G"));
		tXmax.SetText(lMax.x.ToString("G"));
		tYmin.SetText(lMin.y.ToString("G"));
		tYmax.SetText(lMax.y.ToString("G"));
	}
}
