using System.Collections.Generic;
using UnityEngine;

namespace MagicGame
{
    public static class CombatWorld
    {
        public static Rect Bounds => LevelManager.Instance != null
            ? LevelManager.Instance.playableBounds : new Rect(-7.68f, -5.12f, 15.36f, 10.24f);
        public static bool Contains(Vector2 point) => LevelManager.Instance != null
            ? LevelManager.Instance.ContainsPoint(point) : Bounds.Contains(point);

        public static List<Health> Enemies(Vector2 point, float radius, bool visibleOnly = false)
        {
            var result = new List<Health>();
            var camera = visibleOnly ? Camera.main : null;
            foreach (var enemy in LevelManager.Enemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.Health == null || enemy.Health.IsDead) continue;
                Vector2 pos = enemy.transform.position;
                if (Vector2.Distance(point, pos) > radius || !Contains(pos)) continue;
                if (visibleOnly && camera == null) continue;
                if (camera != null)
                {
                    var viewport = camera.WorldToViewportPoint(pos);
                    if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
                }
                result.Add(enemy.Health);
            }
            return result;
        }

        public static Vector2 Clamp(Vector2 point, float margin = 0.1f)
        {
            var bounds = Bounds;
            return new Vector2(Mathf.Clamp(point.x, bounds.xMin + margin, bounds.xMax - margin),
                Mathf.Clamp(point.y, bounds.yMin + margin, bounds.yMax - margin));
        }

        public static Vector2 GroundTarget(Vector2 from, Vector2 target, float clearance = 0.1f)
        {
            target = Clamp(target, clearance);
            Vector2 delta = target - from;
            foreach (var hit in Physics2D.RaycastAll(from, delta.normalized, delta.magnitude))
            {
                var wall = hit.collider.GetComponent<Wall>();
                if (wall == null || !wall.isBoundary) continue;
                target = hit.point + hit.normal * Mathf.Max(0.15f, clearance);
                break;
            }
            // A center can be inside the map while its projectile hitbox still overlaps an
            // angled wall. Resolve the full clearance circle, including adjoining corners.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                bool adjusted = false;
                foreach (var collider in Physics2D.OverlapCircleAll(target, clearance))
                {
                    var wall = collider.GetComponent<Wall>();
                    if (wall == null || !wall.isBoundary) continue;
                    Vector2 closest = collider.ClosestPoint(target);
                    Vector2 away = target - closest;
                    if (away.sqrMagnitude < 0.000001f || !Contains(target)) away = from - closest;
                    if (away.sqrMagnitude < 0.000001f) continue;
                    target = closest + away.normalized * (clearance + 0.005f);
                    adjusted = true;
                }
                if (!adjusted) break;
            }
            return Contains(target) ? target : from;
        }

        public static Vector2 Move(Rigidbody2D body, Vector2 delta)
        {
            if (delta.sqrMagnitude < 0.000001f) return body.position;
            var filter = new ContactFilter2D();
            filter.NoFilter();
            filter.useTriggers = false;
            var hits = new RaycastHit2D[24];
            int count = body.Cast(delta.normalized, filter, hits, delta.magnitude + 0.03f);
            float distance = delta.magnitude;
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null) distance = Mathf.Min(distance, Mathf.Max(0f, hits[i].distance - 0.03f));
            Vector2 point = Clamp(body.position + delta.normalized * distance, 0.15f);
            return Contains(point) ? point : body.position;
        }

        public static bool IsSafe(Vector2 point, Rigidbody2D body)
        {
            if (!Contains(point)) return false;
            var feet = body.GetComponent<CircleCollider2D>();
            float radius = feet != null ? feet.radius * Mathf.Max(body.transform.lossyScale.x, body.transform.lossyScale.y) : 0.3f;
            Vector2 center = point + (feet != null ? feet.offset : Vector2.zero);
            foreach (var collider in Physics2D.OverlapCircleAll(center, radius + 0.05f))
                if (!collider.isTrigger && collider.attachedRigidbody != body) return false;
            return true;
        }

        public static Vector2 SafeTeleport(Rigidbody2D body, float range)
        {
            Vector2 best = body.position;
            float bestClearance = -1f;
            for (int ring = 1; ring <= 3; ring++)
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f;
                Vector2 candidate = body.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * range * ring / 3f;
                if (!IsSafe(candidate, body)) continue;
                // Require an unobstructed path so teleport cannot cross sealed geometry.
                if (Vector2.Distance(Move(body, candidate - body.position), candidate) > 0.1f) continue;
                float clearance = range * 2f;
                foreach (var enemy in LevelManager.Enemies)
                    if (enemy != null && enemy.Health != null && !enemy.Health.IsDead)
                        clearance = Mathf.Min(clearance, Vector2.Distance(candidate, enemy.transform.position));
                if (clearance > bestClearance) { best = candidate; bestClearance = clearance; }
            }
            return best;
        }
    }
}
