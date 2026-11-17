using Game.Actors;
using UnityEngine;

namespace Game.StreetMap
{
    public sealed class FixedMapPortal : MonoBehaviour
    {
        private SpriteRenderer skin;
        public bool IsOpen {get;private set;}
        public void Initialize(Sprite sprite)
        {skin=gameObject.AddComponent<SpriteRenderer>();skin.sprite=sprite;skin.color=Color.cyan;skin.sortingOrder=4;skin.enabled=false;}
        public bool TryOpen(PlayerCombatFacade hero)
        {
            if(IsOpen)return true;
            if(hero==null || hero.IsDead)return false;
            var room=StreetMapLayout.Rooms[4];
            Vector2 origin=hero.transform.position;
            if(!room.Bounds.Contains(Vector2Int.FloorToInt(origin)))origin=room.Center;
            for(int ring=0;ring<4;ring++)for(int step=0;step<16;step++)
            {
                float angle=step*Mathf.PI/8;
                var point=origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(2.5f+ring);
                if(!Available(point,origin))continue;
                transform.position=point;IsOpen=true;skin.enabled=true;return true;
            }
            return false;
        }
        public static bool Available(Vector2 point,Vector2 origin)
        {
            var r=StreetMapLayout.Rooms[4].Bounds;
            if(point.x<r.xMin+1 || point.x>r.xMax-1 || point.y<r.yMin+1 || point.y>r.yMax-1)return false;
            Physics2D.SyncTransforms();
            if(Physics2D.CircleCast(origin,.45f,(point-origin).normalized,Vector2.Distance(point,origin),LayerMask.GetMask("World")))return false;
            foreach(var collider in Physics2D.OverlapCircleAll(point,.7f))
            {
                if(collider.isTrigger)continue;
                if(collider.gameObject.layer==LayerMask.NameToLayer("World"))return false;
                var receiver=collider.GetComponentInParent<DamageReceiver>();if(receiver!=null && !receiver.IsDead)return false;
            }
            return true;
        }
    }
}
