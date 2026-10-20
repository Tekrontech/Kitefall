"""
Kitefall: GLB -> FBX batch converter for Blender 4.x (free).

Kya karta hai:
  1. models/ folder ki har <id>.glb ko import karta hai
  2. fbx/<id>.fbx export karta hai (mesh + rig + saari 11 animations, Unity ke liye)
  3. rookie_m se har animation alag file mein: fbx/Anim/anim_<naam>.fbx (sirf rig, "Without Skin")

Chalane ka tareeqa (2 mein se koi ek):
  A) Blender kholen > Scripting tab > Open > yeh file > neeche MODELS_DIR ka path theek karein > Run Script (▶)
  B) Terminal:  blender -b -P convert_to_fbx_blender.py -- "C:/path/to/models"
"""
import bpy, os, sys

MODELS_DIR = r"C:/Kitefall_3D/models"          # <-- apna path yahan likhein (option A ke liye)
if "--" in sys.argv:
    MODELS_DIR = sys.argv[sys.argv.index("--") + 1]
OUT_DIR = os.path.join(os.path.dirname(MODELS_DIR.rstrip("/\\")), "fbx")
ANIM_DIR = os.path.join(OUT_DIR, "Anim")
ANIM_SOURCE = "rookie_m"
os.makedirs(ANIM_DIR, exist_ok=True)


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for coll in (bpy.data.actions, bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images):
        for b in list(coll):
            coll.remove(b)


def fbx_export(path, **kw):
    bpy.ops.export_scene.fbx(filepath=path, use_selection=False, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             add_leaf_bones=False, bake_anim=True, bake_anim_use_nla_strips=False,
                             bake_anim_simplify_factor=0.0, path_mode="COPY", embed_textures=True, **kw)


files = sorted(f for f in os.listdir(MODELS_DIR) if f.lower().endswith(".glb"))
for f in files:
    cid = os.path.splitext(f)[0]
    clear()
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS_DIR, f))
    arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    if arm.animation_data is None:
        arm.animation_data_create()
    # character FBX with every clip as a separate take
    fbx_export(os.path.join(OUT_DIR, cid + ".fbx"), bake_anim_use_all_actions=True)
    print("OK", cid)
    if cid == ANIM_SOURCE:
        for act in list(bpy.data.actions):
            arm.animation_data.action = act
            clips = ["fly_hold", "idle", "walk", "run", "jump", "climb", "victory", "clap", "wave", "dance", "sad"]
            name = next((c for c in clips if act.name.startswith(c)), act.name.split(".")[0])
            sc = bpy.context.scene
            sc.frame_start, sc.frame_end = int(act.frame_range[0]), int(act.frame_range[1])
            fbx_export(os.path.join(ANIM_DIR, "anim_" + name + ".fbx"), object_types={"ARMATURE"},
                       bake_anim_use_all_actions=False)
            print("  anim", name)
print("DONE ->", OUT_DIR)
