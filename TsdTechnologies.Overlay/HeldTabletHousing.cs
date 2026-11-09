using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TsdTechnologies.Overlay.Animation
{
    // Physical tablet housing. Original pooled asset is restored on return.
    public sealed class HeldTabletHousing : MonoBehaviour
    {
        private GameObject housing;
        private Renderer[] originals;
        private bool[] states;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private Material linkLed, activityLed;
        private bool? lastLink, lastActivity;

        public void Begin()
        {
            Restore();
            originals = GetComponentsInChildren<Renderer>(true);
            states = originals.Select(r => r.enabled).ToArray();
            var seed = originals.Select(r => r.sharedMaterial).FirstOrDefault(m => m != null);
            if(seed == null) throw new InvalidOperationException("No tablet material available for housing preview.");
            housing = new GameObject("TSD_Rounded_Housing");
            housing.transform.SetParent(transform, false);
            var shell = MakeMaterial(seed, new Color(.09f,.105f,.115f,1f), .05f, .12f);
            var rubber = MakeMaterial(seed, new Color(.025f,.028f,.03f,1f), .02f, .2f);
            var rim = MakeMaterial(seed, new Color(.17f,.19f,.20f,1f), .1f, .18f);
            var rear = MakeMaterial(seed,new Color(.018f,.02f,.022f,1f),0f,0f);
            rear.name="TSD_Matte_Black_Rear";
            foreach(var material in new[]{rear,shell,rubber,rim})
            {
                if(material.HasProperty("_SpecularHighlights"))material.SetFloat("_SpecularHighlights",0f);
                if(material.HasProperty("_GlossyReflections"))material.SetFloat("_GlossyReflections",0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            }
            var screen = MakeMaterial(seed, new Color(.008f,.012f,.015f,1f), .12f, .65f);
            var accent = MakeMaterial(seed, new Color(.72f,.37f,.08f,1f), .25f, .45f);
            var gold = MakeMaterial(seed, new Color(.68f,.48f,.19f,1f), .5f, .4f);


            // Rectangular OS casing; slim inset corner plates stay inside the tested footprint.
            Part("Rubber perimeter", .294f,.202f,.019f,.002f, Vector3.zero, rubber);
            Part("Bevelled metal shell", .286f,.194f,.021f,.0018f, new Vector3(0,0,.001f), rim);
            Part("Graphite face", .279f,.187f,.022f,.0015f, new Vector3(0,0,.002f), shell);
            Part("Screen gasket", .247f,.153f,.003f,.0015f, new Vector3(0,0,.014f), rubber);
            Part("Dark display", .237f,.143f,.0012f,.001f, new Vector3(0,0,.016f), screen);
            // A single matte rear cover sits behind all front layers; no coplanar faces.
            Part("Matte black rear cover", .29f,.198f,.003f,.002f, new Vector3(0,0,-.012f), rear);
            // The same layout as the OS housing, with all hardware kept inside
            // the rectangular silhouette. Positive local X appears on the viewer's left.
            Part("Top engraved nameplate", .09f,.012f,.0018f,.0025f, new Vector3(0,.085f,.0145f), rim);
            Label("TSD // FIELD TERMINAL", new Vector3(0,.085f,.0156f), .00048f, gold);
            Part("Top rail", .235f,.0018f,.001f,.0007f, new Vector3(0,.095f,.014f), rim);
            Part("Bottom rail", .235f,.0018f,.001f,.0007f, new Vector3(0,-.095f,.014f), rim);
            foreach(float y in new[]{.091f,.079f,-.079f,-.091f})
                Part("Inset horizontal rail",.235f,.0011f,.0008f,.0004f,new Vector3(0,y,.0145f),rim);
            foreach(float x in new[]{-.137f,.137f})
            {
                Part("Vertical bezel rail", .0018f,.14f,.001f,.0007f, new Vector3(x,0,.014f), rim);
                Part("Rail recessed channel", .00065f,.136f,.0006f,.00025f, new Vector3(x,0,.0148f), rubber);
            }
            Part("Link indicator plate", .041f,.008f,.0012f,.002f, new Vector3(-.098f,.085f,.0145f), rubber);
            Label("TSD LINK", new Vector3(-.096f,.085f,.0153f), .00038f, rim);
            linkLed = MakeMaterial(seed,new Color(.015f,.035f,.02f,1f),.05f,.55f);linkLed.name="TSD_Link_LED";
            activityLed = MakeMaterial(seed,new Color(.015f,.035f,.02f,1f),.05f,.55f);activityLed.name="TSD_Activity_LED";
            // Signal glyph on the viewer's right, activity lamp on the left, like Field OS.
            SignalIcon(new Vector3(-.114f,.083f,.016f),linkLed);
            Part("Activity LED", .0023f,.0023f,.0008f,.001f, new Vector3(-.079f,.085f,.0158f), activityLed);

            Part("Left function bank", .059f,.012f,.0018f,.0022f, new Vector3(.087f,-.085f,.0145f), rubber);
            Button("MODE",.106f,-.085f,.0155f,rim,gold);
            Button("MAP",.087f,-.085f,.0155f,rim,gold);
            Button("LOG",.068f,-.085f,.0155f,rim,gold);
            Part("Right device control bank", .103f,.012f,.0018f,.0022f, new Vector3(-.06f,-.085f,.0145f), rubber);
            Label("VOL",new Vector3(-.022f,-.085f,.0156f),.0004f,rim);
            Button("-",-.033f,-.085f,.0155f,shell,gold,.007f);
            Button("+",-.043f,-.085f,.0155f,shell,gold,.007f);
            Label("BRT",new Vector3(-.058f,-.085f,.0156f),.0004f,rim);
            Button("-",-.069f,-.085f,.0155f,shell,gold,.007f);
            Button("+",-.079f,-.085f,.0155f,shell,gold,.007f);
            Button("MENU",-.09f,-.085f,.0155f,rim,gold,.014f);
            Part("Power button rim",.009f,.009f,.0018f,.0044f,new Vector3(-.109f,-.085f,.015f),gold);
            Part("Power button face",.0068f,.0068f,.0012f,.0033f,new Vector3(-.109f,-.085f,.0165f),rubber);
            Part("Power symbol ring",.0044f,.0044f,.0005f,.0021f,new Vector3(-.109f,-.085f,.0173f),gold);
            Part("Power symbol centre",.003f,.003f,.0005f,.0014f,new Vector3(-.109f,-.085f,.0177f),rubber);
            Part("Power symbol stem",.0006f,.0027f,.0005f,.0002f,new Vector3(-.109f,-.0834f,.0181f),gold);

            // Small inset corner details remain within the continuous housing.
            foreach(float x in new[]{-.13f,.13f}) foreach(float y in new[]{-.086f,.086f})
            {
                Part("Slim corner plate",.026f,.028f,.0016f,.0015f,new Vector3(x,y,.0143f),rubber);
                Part("Inset corner plate face",.023f,.025f,.0005f,.001f,new Vector3(x,y,.0153f),shell);
                Part("Recessed fastener", .004f,.004f,.001f,.0019f, new Vector3(x,y,.016f), rubber);
                Part("Fastener slot", .0022f,.00045f,.0004f,.00018f, new Vector3(x,y,.0167f), rim);
            }
            for(int i=0;i<3;i++)
                Part("Side control", .004f,.011f,.004f,.0015f, new Vector3(.145f,.025f-i*.019f,0), i==0?accent:rubber);
            Part("Bottom data port", .015f,.0018f,.0007f,.0008f, new Vector3(0,-.09f,.014f), rubber);

            CombineByMaterial();
            foreach(var child in housing.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = gameObject.layer;
            foreach(var renderer in originals) renderer.enabled = false;
        }

        private void CombineByMaterial()
        {
            foreach(var group in housing.GetComponentsInChildren<MeshRenderer>().GroupBy(r=>r.sharedMaterial).ToArray())
            {
                var parts=group.ToArray();
                if(parts.Length<2)continue;
                var combine=parts.Select(r=>new CombineInstance{
                    mesh=r.GetComponent<MeshFilter>().sharedMesh,
                    transform=housing.transform.worldToLocalMatrix*r.transform.localToWorldMatrix
                }).ToArray();
                var mesh=new Mesh{name="TSD_Housing_Material_Group"};mesh.CombineMeshes(combine,true,true);meshes.Add(mesh);
                var obj=new GameObject("Housing material group");obj.transform.SetParent(housing.transform,false);
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
                foreach(var part in parts){part.enabled=false;Release(part.gameObject);}
            }
        }

        public void SetIndicators(bool powered,bool connected,bool busy,float time)
        {
            bool link=powered&&connected;
            if(lastLink!=link){SetLed(linkLed,link);lastLink=link;}
            bool pulse=busy&&((time% .71f)<.09f||((time% .71f)>.19f&&(time% .71f)<.25f));
            bool activity=powered&&pulse;
            if(lastActivity!=activity){SetLed(activityLed,activity);lastActivity=activity;}
        }
        private static void SetLed(Material material,bool on)
        {
            if(material==null)return;
            material.color=on?new Color(.18f,.85f,.35f,1f):new Color(.015f,.035f,.02f,1f);
            if(material.HasProperty("_EmissionColor"))
            {
                if(on)material.EnableKeyword("_EMISSION");else material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor",on?new Color(.12f,1.3f,.28f,1f):Color.black);
            }
        }

        private static void Release(UnityEngine.Object value)
        {
#if UNITY_EDITOR
            if(!Application.isPlaying){UnityEngine.Object.DestroyImmediate(value);return;}
#endif
            UnityEngine.Object.Destroy(value);
        }

#if UNITY_EDITOR
        public GameObject DetachForAuthoring()
        {
            var result=housing;housing=null;originals=null;states=null;
            meshes.Clear();materials.Clear();linkLed=null;activityLed=null;
            return result;
        }
#endif

        private void SignalIcon(Vector3 origin,Material material)
        {
            Part("Signal dot",.0012f,.0012f,.0005f,.00055f,origin,material);
            for(int arc=0;arc<3;arc++)
            {
                float radius=.0019f+arc*.00125f;
                for(int segment=0;segment<10;segment++)
                {
                    float a=(-55f+segment*11f)*Mathf.Deg2Rad;
                    float b=a+11f*Mathf.Deg2Rad;
                    var from=origin+new Vector3(Mathf.Sin(a)*radius,Mathf.Cos(a)*radius,0);
                    var to=origin+new Vector3(Mathf.Sin(b)*radius,Mathf.Cos(b)*radius,0);
                    var bar=Part("Signal arc",(to-from).magnitude+.00015f,.00045f,.0005f,.0002f,(from+to)*.5f,material);
                    bar.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg);
                }
            }
        }

        private void Button(string text,float x,float y,float z,Material face,Material ink,float width=.016f)
        {
            Part("Device button "+text,width,.007f,.0012f,.0012f,new Vector3(x,y,z),face);
            Label(text,new Vector3(x,y,z+.00075f),.00036f,ink);
        }

        // Tiny engraved labels use static geometry, avoiding runtime font assets.
        private static readonly Dictionary<char,string[]> Glyphs = new Dictionary<char,string[]>
        {
            ['T']=new[]{"11111","00100","00100","00100","00100","00100","00100"},
            ['S']=new[]{"01111","10000","10000","01110","00001","00001","11110"},
            ['D']=new[]{"11110","10001","10001","10001","10001","10001","11110"},
            ['F']=new[]{"11111","10000","10000","11110","10000","10000","10000"},
            ['I']=new[]{"11111","00100","00100","00100","00100","00100","11111"},
            ['E']=new[]{"11111","10000","10000","11110","10000","10000","11111"},
            ['L']=new[]{"10000","10000","10000","10000","10000","10000","11111"},
            ['R']=new[]{"11110","10001","10001","11110","10100","10010","10001"},
            ['M']=new[]{"10001","11011","10101","10101","10001","10001","10001"},
            ['N']=new[]{"10001","11001","11001","10101","10011","10011","10001"},
            ['A']=new[]{"01110","10001","10001","11111","10001","10001","10001"},
            ['O']=new[]{"01110","10001","10001","10001","10001","10001","01110"},
            ['P']=new[]{"11110","10001","10001","11110","10000","10000","10000"},
            ['G']=new[]{"01111","10000","10000","10111","10001","10001","01111"},
            ['V']=new[]{"10001","10001","10001","10001","10001","01010","00100"},
            ['B']=new[]{"11110","10001","10001","11110","10001","10001","11110"},
            ['U']=new[]{"10001","10001","10001","10001","10001","10001","01110"},
            ['K']=new[]{"10001","10010","10100","11000","10100","10010","10001"},
            ['/']=new[]{"00001","00010","00010","00100","01000","01000","10000"},
            ['+']=new[]{"00000","00100","00100","11111","00100","00100","00000"},
            ['-']=new[]{"00000","00000","00000","11111","00000","00000","00000"}
        };

        private void Label(string text,Vector3 position,float pixel,Material material)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            float start=(text.Length*6-1)*pixel*.5f;
            for(int c=0;c<text.Length;c++)
            {
                if(!Glyphs.TryGetValue(text[c],out var rows))continue;
                for(int row=0;row<7;row++)for(int col=0;col<5;col++)if(rows[row][col]=='1')
                {
                    float x=start-(c*6+col)*pixel,y=(3-row)*pixel;
                    int n=vertices.Count;
                    vertices.Add(new Vector3(x-pixel*.85f,y-pixel*.4f,0));vertices.Add(new Vector3(x,y-pixel*.4f,0));
                    vertices.Add(new Vector3(x,y+pixel*.4f,0));vertices.Add(new Vector3(x-pixel*.85f,y+pixel*.4f,0));
                    triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
                }
            }
            var mesh=new Mesh{name="TSD_Engraved_Label"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var obj=new GameObject("Engraving "+text);obj.transform.SetParent(housing.transform,false);obj.transform.localPosition=position;
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        private Material MakeMaterial(Material seed, Color color, float metallic, float smoothness)
        {
            var material = new Material(seed) { name = "TSD_Held_Housing", color = color };
            if(material.HasProperty("_Metallic")) material.SetFloat("_Metallic",metallic);
            if(material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness",smoothness);
            materials.Add(material);
            return material;
        }

        private GameObject Part(string name,float width,float height,float depth,float radius,Vector3 position,Material material)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(housing.transform,false);
            obj.transform.localPosition = position;
            var mesh = RoundedBox(width,height,depth,radius);
            meshes.Add(mesh);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }

        internal static Mesh RoundedBox(float width,float height,float depth,float radius)
        {
            const int steps=8;
            var points=new List<Vector2>();
            radius=Mathf.Min(radius,Mathf.Min(width,height)*.5f);
            for(int corner=0;corner<4;corner++)
            {
                float angle=corner*90f;
                float cx=(corner==0||corner==3?1:-1)*(width*.5f-radius);
                float cy=(corner<2?1:-1)*(height*.5f-radius);
                for(int i=0;i<=steps;i++)
                {
                    float a=(angle+i*90f/steps)*Mathf.Deg2Rad;
                    points.Add(new Vector2(cx+Mathf.Cos(a)*radius,cy+Mathf.Sin(a)*radius));
                }
            }
            int n=points.Count;
            var vertices=new List<Vector3>();
            var triangles=new List<int>();
            // Independent cap/side vertices keep flat screen normals and smooth sides.
            vertices.Add(new Vector3(0,0,depth*.5f));
            vertices.AddRange(points.Select(p=>new Vector3(p.x,p.y,depth*.5f)));
            vertices.Add(new Vector3(0,0,-depth*.5f));
            vertices.AddRange(points.Select(p=>new Vector3(p.x,p.y,-depth*.5f)));
            int side=vertices.Count;
            foreach(var p in points){vertices.Add(new Vector3(p.x,p.y,depth*.5f));vertices.Add(new Vector3(p.x,p.y,-depth*.5f));}
            for(int i=0;i<n;i++)
            {
                int next=(i+1)%n;
                triangles.AddRange(new[]{0,i+1,next+1,n+1,n+2+next,n+2+i});
                int a=side+2*i,b=side+2*next;
                triangles.AddRange(new[]{a,a+1,b,b,a+1,b+1});
            }
            var mesh=new Mesh{name="TSD_Rounded_Tablet_Part"};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }

        public void Restore()
        {
            if(housing!=null){housing.SetActive(false);Release(housing);housing=null;}
            if(originals!=null)for(int i=0;i<originals.Length;i++)if(originals[i]!=null)originals[i].enabled=states[i];
            originals=null;states=null;
            foreach(var mesh in meshes)if(mesh!=null)Release(mesh);
            foreach(var material in materials)if(material!=null)Release(material);
            meshes.Clear();materials.Clear();
            linkLed=null;activityLed=null;lastLink=null;lastActivity=null;
        }
        private void OnDisable(){Restore();}
        private void OnDestroy(){Restore();}
    }
}
