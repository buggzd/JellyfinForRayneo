using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class AxisRoutingScrollRect : ScrollRect
    {
        private bool _dragTargetResolved;
        private bool _dragTargetStarted;
        private bool _routeToParent;

        public ScrollRect ParentScrollRect { get; private set; }

        public void ConfigureParent(ScrollRect parentScrollRect)
        {
            ParentScrollRect = parentScrollRect;
        }

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            ResetDragTarget();
            base.OnInitializePotentialDrag(eventData);
            if (CanRouteToParent())
            {
                ParentScrollRect.OnInitializePotentialDrag(eventData);
            }
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            ResetDragTarget();
            ResolveDragTarget(eventData);
            BeginResolvedTarget(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (!_dragTargetResolved)
            {
                ResolveDragTarget(eventData);
            }
            if (!_dragTargetResolved)
            {
                return;
            }

            BeginResolvedTarget(eventData);
            if (_routeToParent)
            {
                ParentScrollRect.OnDrag(eventData);
            }
            else
            {
                base.OnDrag(eventData);
            }
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (_dragTargetStarted)
            {
                if (_routeToParent && CanRouteToParent())
                {
                    ParentScrollRect.OnEndDrag(eventData);
                }
                else
                {
                    base.OnEndDrag(eventData);
                }
            }
            ResetDragTarget();
        }

        public override void OnScroll(PointerEventData eventData)
        {
            bool verticalIntent = Mathf.Abs(eventData.scrollDelta.y) >= Mathf.Abs(eventData.scrollDelta.x);
            if (verticalIntent && CanRouteToParent())
            {
                ParentScrollRect.OnScroll(eventData);
                return;
            }
            base.OnScroll(eventData);
        }

        protected override void OnDisable()
        {
            ResetDragTarget();
            base.OnDisable();
        }

        private void ResolveDragTarget(PointerEventData eventData)
        {
            Vector2 movement = eventData.position - eventData.pressPosition;
            if (movement.sqrMagnitude < 0.01f)
            {
                movement = eventData.delta;
            }
            if (movement.sqrMagnitude < 0.01f)
            {
                return;
            }

            bool verticalIntent = Mathf.Abs(movement.y) >= Mathf.Abs(movement.x);
            _routeToParent = verticalIntent && CanRouteToParent();
            _dragTargetResolved = true;
        }

        private void BeginResolvedTarget(PointerEventData eventData)
        {
            if (!_dragTargetResolved || _dragTargetStarted)
            {
                return;
            }

            if (_routeToParent)
            {
                ParentScrollRect.OnBeginDrag(eventData);
            }
            else
            {
                base.OnBeginDrag(eventData);
            }
            _dragTargetStarted = true;
        }

        private bool CanRouteToParent()
        {
            return ParentScrollRect != null
                && ParentScrollRect != this
                && ParentScrollRect.isActiveAndEnabled
                && ParentScrollRect.vertical;
        }

        private void ResetDragTarget()
        {
            _dragTargetResolved = false;
            _dragTargetStarted = false;
            _routeToParent = false;
        }
    }
}
