#nullable disable
using System;
using System.Linq;
using EFT;
using EFT.AssetsManager;
using EFT.InventoryLogic;
using TsdTechnologies.Api;
using UnityEngine;

namespace TsdTechnologies.Client
{
    internal static class TabletDropPhysics
    {
        internal static int Repair(GameObject root,Item item)
        {
            if(root==null||item==null||item.StringTemplateId!=TabletRuntime.TerminalTemplateId)return 0;
            var pool=root.GetComponent<AssetPoolObject>();
            if(pool==null)return 0;
            int repaired=0;
            foreach(var source in pool.Colliders.OfType<BoxCollider>().Where(c=>c!=null&&c.gameObject==root&&!c.isTrigger).ToArray())
            {
                // LootItem.Init puts the root on Interactive and children on Loot.
                // Keep its pickup trigger on the root; move only the solid shape.
                var child=new GameObject("TSD_Loot_Physics");
                child.transform.SetParent(root.transform,false);
                child.layer=LayersMaskController.LootLayer;
                var replacement=child.AddComponent<BoxCollider>();
                replacement.center=source.center;
                replacement.size=source.size;
                replacement.sharedMaterial=source.sharedMaterial;
                replacement.contactOffset=source.contactOffset;
                replacement.isTrigger=false;
                replacement.enabled=true;
                for(int i=0;i<pool.Colliders.Count;i++)if(pool.Colliders[i]==source)pool.Colliders[i]=replacement;
                // The factory may already have cached this collider's pool state.
                // Transfer those references so returning the item does not re-enable
                // its obsolete solid root collider or reset the child to Interactive.
                foreach(var saved in pool.RegisteredCollidersToDisable.Where(s=>s.Collider==source))
                {saved.Collider=replacement;saved.Layer=child.layer;}
                source.enabled=false;
                repaired++;
            }
            return repaired;
        }
    }
}
