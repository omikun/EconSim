using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraphAxis : MonoBehaviour {

	GameObject pymax, pymin, pxmax;
	// Use this for initialization
	void Start () {
		pymax = transform.parent.Find("pymax").gameObject;
		pymin = transform.parent.Find("pymin").gameObject;
		pxmax = transform.parent.Find("pxmax").gameObject;

		var axis = GetComponent<LineRenderer>();
		axis.numPositions = 3;
		axis.SetPosition(0, pymax.transform.position);
		axis.SetPosition(1, pymin.transform.position);
		axis.SetPosition(2, pxmax.transform.position);
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
