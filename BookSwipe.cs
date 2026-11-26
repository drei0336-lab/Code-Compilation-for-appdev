using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ARCHIV3D
{
    /// <summary>Lets the player swipe the open book left/right to turn pages.</summary>
    public class BookSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action onSwipeLeft;
        public Action onSwipeRight;
        [Tooltip("How far (in screen pixels) a drag must go to count as a swipe.")]
        public float minDistance = 60f;

        Vector2 _start;

        public void OnBeginDrag(PointerEventData e) { _start = e.position; }
        public void OnDrag(PointerEventData e) { }

        public void OnEndDrag(PointerEventData e)
        {
            Vector2 delta = e.position - _start;
            if (Mathf.Abs(delta.x) < minDistance || Mathf.Abs(delta.x) < Mathf.Abs(delta.y)) return;
            if (delta.x < 0) onSwipeLeft?.Invoke();
            else onSwipeRight?.Invoke();
        }
    }
}
