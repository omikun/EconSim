using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Linq;

public class GraphMe : MonoBehaviour {
	LineRenderer line;
	List<float> inputs = new List<float>();
	Vector2 lMin = new Vector2();
	Vector2 lMax = new Vector2();
	//physical min/max
	GraphLead gl;
		// Use this for initialization
	void Start () {
		gl = transform.parent.GetComponent<GraphLead>();

        lMin = Vector2.zero;
		lMax = new Vector2(gl.maxPoints, 10);

		Assert.IsNotNull(gl);
		line = GetComponent<LineRenderer>();
		line.numPositions = gl.maxPoints;
    }
	
	// Update is called once per frame
	public void Tick (float input) {
		lMax.y = Mathf.Max(lMax.y, input);
		lMin.y = Mathf.Min(lMin.y, input);
		//update x bounds, remove last point
		if (inputs.Count >= gl.maxPoints)
		{
			if (lMax.y == inputs[0])
			{
				//scan for max point
				float newMax = inputs.Max();
				gl.SetMaxY(newMax);
			}
			inputs.RemoveAt(0);
			lMin.x += 1;
            lMax.x += 1;
		}
        for (int i = 0; i < inputs.Count; i++)
        {
            line.SetPosition(i, gl.L2PC_RelX(i, inputs[i]));
        }
		inputs.Add(input);
        int x = inputs.Count-1;
		var lastPos = gl.L2PC_RelX(x, input);
        line.SetPosition(x, lastPos);
		//set all points after
        for (int i = x + 1; i < gl.maxPoints; i++)
        {
            line.SetPosition(i, lastPos);
        }

		//set text
		gl.UpdateLBounds(lMin, lMax);
	}
}
