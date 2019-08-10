using UnityEngine;

namespace UnityCodePolicy
{
	public class BoxBehaviour : MonoBehaviour
	{
		private void Start()
		{
			var rnd = Mathf.Ceil(Random.Range(0f, 1f));

			if (rnd == 1)
				Debug.Log(rnd);
		}
	}
}
