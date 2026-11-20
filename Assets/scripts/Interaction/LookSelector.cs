using PixelCrushers.DialogueSystem;
using UnityEngine;

/// <summary>
/// Dialogue System Selector that offers exactly what the crosshair is on. The stock selector raycasts only its
/// layer mask (here just Default, so doors and props on other layers were see-through and people behind them got
/// selected) and counts trigger volumes (count zones, door triggers) as hits. This one casts against every layer
/// except Ignore Raycast, skips triggers and the player's own colliders, and uses the nearest solid hit: if that
/// isn't usable, nothing is selected. Drop-in replacement: same serialized fields as the stock Selector.
/// </summary>
[AddComponentMenu("Lil Windows/Look Selector")]
public class LookSelector : PixelCrushers.DialogueSystem.Wrappers.Selector
{
    private const int IgnoreRaycastLayer = 2;
    private readonly RaycastHit[] hits = new RaycastHit[32];

    protected override void Run3DRaycast()
    {
        Camera camera = Camera.main;
        if(camera == null)
        {
            DeselectTarget();
            return;
        }

        Ray ray = camera.ScreenPointToRay(GetSelectionPoint());
        lastRay = ray;
        int count = Physics.RaycastNonAlloc(ray, hits, maxSelectionDistance, ~(1 << IgnoreRaycastLayer), QueryTriggerInteraction.Ignore);

        //nearest hit that isn't part of the player
        int nearest = -1;
        for(int i = 0; i < count; i++)
        {
            if(hits[i].collider.transform.IsChildOf(transform)) continue;
            if(nearest < 0 || hits[i].distance < hits[nearest].distance) nearest = i;
        }
        if(nearest < 0)
        {
            DeselectTarget();
            return;
        }

        RaycastHit hit = hits[nearest];
        lastHit = hit;
        distance = hit.distance;
        Usable hitUsable = hit.collider.GetComponent<Usable>();
        if(hitUsable == null) hitUsable = hit.collider.GetComponentInParent<Usable>();
        if(hitUsable != null && hitUsable.enabled)
        {
            if(selection != hitUsable.gameObject) SetCurrentUsable(hitUsable);
        }
        else
        {
            //a wall, a door, or a prop in front of the person: nothing to offer
            DeselectTarget();
        }
    }
}
