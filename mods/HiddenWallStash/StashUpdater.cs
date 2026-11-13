using UnityEngine;

public class StashUpdater : MonoBehaviour
{
	private void Update()
	{
		StashManager.Tick();
	}
}
