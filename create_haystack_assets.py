"""
Generate 3D Assets for Needle in a Haystack Simulator
Blender 5.0 Headless Script
Outputs FBX models to Assets/Models/
"""

import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix, Euler

OUTPUT_DIR = os.path.abspath("Assets/Models")
os.makedirs(OUTPUT_DIR, exist_ok=True)

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Ensure a collection exists
    if not bpy.data.collections:
        col = bpy.data.collections.new("Collection")
        bpy.context.scene.collection.children.link(col)

def export_fbx(obj, filename):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    
    filepath = os.path.join(OUTPUT_DIR, filename)
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        global_scale=1.0,
        apply_unit_scale=True,
        bake_space_transform=True,
        axis_forward='-Z',
        axis_up='Y',
        mesh_smooth_type='FACE'
    )
    print(f"Exported: {filepath} ({os.path.getsize(filepath)} bytes)")

def create_straw():
    reset_scene()
    mesh = bpy.data.meshes.new("Straw_LowPoly")
    obj = bpy.data.objects.new("Straw_LowPoly", mesh)
    bpy.context.scene.collection.objects.link(obj)
    
    bm = bmesh.new()
    
    # 5 connected segments with a slight natural bend and wheat nodes
    num_segments = 5
    total_len = 0.36
    seg_len = total_len / num_segments
    r_base = 0.0065
    radial_segments = 8
    
    # Generate straw rings along a slightly curved spline
    rings = []
    bend_amplitude = 0.025
    for i in range(num_segments + 1):
        t = i / float(num_segments) # 0 to 1
        y = (t - 0.5) * total_len
        # Parabolic gentle bend along X
        x = (4.0 * t * (1.0 - t)) * bend_amplitude
        # Slight taper at tips, subtle bulge at nodes (t=0.33, 0.66)
        node_bulge = 1.0 + 0.15 * math.exp(-pow((t - 0.35) * 15.0, 2)) + 0.15 * math.exp(-pow((t - 0.70) * 15.0, 2))
        tip_taper = math.sin(t * math.pi) * 0.3 + 0.7
        r = r_base * node_bulge * tip_taper
        
        ring_verts = []
        for a in range(radial_segments):
            angle = (a / float(radial_segments)) * 2.0 * math.pi
            vx = x + r * math.cos(angle)
            vz = r * math.sin(angle)
            v = bm.verts.new(Vector((vx, y, vz)))
            ring_verts.append(v)
        rings.append(ring_verts)
    
    # Create side faces
    for i in range(num_segments):
        r1 = rings[i]
        r2 = rings[i + 1]
        for a in range(radial_segments):
            a_next = (a + 1) % radial_segments
            bm.faces.new([r1[a], r1[a_next], r2[a_next], r2[a]])
            
    # Cap ends
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    
    # Smooth shading
    for f in bm.faces:
        f.smooth = True
        
    bm.to_mesh(mesh)
    bm.free()
    
    export_fbx(obj, "Straw_LowPoly.fbx")

def create_needle():
    reset_scene()
    mesh = bpy.data.meshes.new("Needle_Golden")
    obj = bpy.data.objects.new("Needle_Golden", mesh)
    bpy.context.scene.collection.objects.link(obj)
    
    bm = bmesh.new()
    
    # Needle geometry: sharp tip at +Y, cylindrical body, flattened eyelet at -Y
    radial_segments = 10
    total_len = 0.38
    r_body = 0.0055
    
    # Segments:
    # y = +0.19: sharp tip (single vert)
    # y = +0.16: tip taper ring
    # y = 0.00: body ring
    # y = -0.14: neck ring
    # y = -0.16 to -0.19: flattened eyelet loop
    
    tip_v = bm.verts.new(Vector((0, total_len * 0.5, 0)))
    
    # Ring 1: Near tip
    r1_verts = []
    y1 = total_len * 0.42
    r1 = r_body * 0.4
    for a in range(radial_segments):
        ang = (a / float(radial_segments)) * 2.0 * math.pi
        r1_verts.append(bm.verts.new(Vector((r1 * math.cos(ang), y1, r1 * math.sin(ang)))))
    # Connect tip to ring 1
    for a in range(radial_segments):
        a_next = (a + 1) % radial_segments
        bm.faces.new([tip_v, r1_verts[a], r1_verts[a_next]])
        
    # Ring 2: Mid body
    r2_verts = []
    y2 = 0.0
    for a in range(radial_segments):
        ang = (a / float(radial_segments)) * 2.0 * math.pi
        r2_verts.append(bm.verts.new(Vector((r_body * math.cos(ang), y2, r_body * math.sin(ang)))))
    for a in range(radial_segments):
        a_next = (a + 1) % radial_segments
        bm.faces.new([r1_verts[a], r1_verts[a_next], r2_verts[a_next], r2_verts[a]])
        
    # Ring 3: Neck before eyelet
    r3_verts = []
    y3 = -total_len * 0.38
    for a in range(radial_segments):
        ang = (a / float(radial_segments)) * 2.0 * math.pi
        # Flatten slightly in Z, widen slightly in X
        vx = r_body * 1.3 * math.cos(ang)
        vz = r_body * 0.6 * math.sin(ang)
        r3_verts.append(bm.verts.new(Vector((vx, y3, vz))))
    for a in range(radial_segments):
        a_next = (a + 1) % radial_segments
        bm.faces.new([r2_verts[a], r2_verts[a_next], r3_verts[a_next], r3_verts[a]])
        
    # Eyelet loop (top end with hole)
    # Create eyelet contour
    eye_top = -total_len * 0.50
    eye_thick = r_body * 0.55
    eye_w = r_body * 1.6
    
    # Outer eye loop
    outer_eye = [
        bm.verts.new(Vector((-eye_w, y3, 0))),
        bm.verts.new(Vector((-eye_w * 1.1, (y3 + eye_top) * 0.5, 0))),
        bm.verts.new(Vector((0, eye_top, 0))),
        bm.verts.new(Vector((eye_w * 1.1, (y3 + eye_top) * 0.5, 0))),
        bm.verts.new(Vector((eye_w, y3, 0)))
    ]
    
    # Bridge to r3
    bm.faces.new(list(reversed(r3_verts)))
    
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    
    for f in bm.faces:
        f.smooth = True
        
    bm.to_mesh(mesh)
    bm.free()
    
    export_fbx(obj, "Needle_Golden.fbx")

def create_barn_crate():
    reset_scene()
    mesh = bpy.data.meshes.new("Hay_Barn_Crate")
    obj = bpy.data.objects.new("Hay_Barn_Crate", mesh)
    bpy.context.scene.collection.objects.link(obj)
    
    bm = bmesh.new()
    
    # Rustic wooden box container for hay
    # Size: 4.0m x 4.0m x 1.2m with thick wooden border planks
    w = 4.0
    h = 1.2
    d = 4.0
    wall_t = 0.16
    
    # Floor slab
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Diagonal((w, wall_t, d, 1.0)) @ Matrix.Translation(Vector((0, wall_t * 0.5, 0))))
    
    # 4 Corner Posts (stout wooden beams)
    post_s = 0.24
    for px in [-w/2 + post_s/2, w/2 - post_s/2]:
        for pz in [-d/2 + post_s/2, d/2 - post_s/2]:
            mat = Matrix.Translation(Vector((px, h * 0.5, pz))) @ Matrix.Diagonal((post_s, h, post_s, 1.0))
            bmesh.ops.create_cube(bm, size=1.0, matrix=mat)
            
    # 4 Walls made of horizontal wood planks
    num_planks = 3
    plank_h = (h - 0.1) / num_planks
    for i in range(num_planks):
        py = 0.1 + (i + 0.5) * plank_h
        # North / South walls
        for pz in [-d/2 + wall_t/2, d/2 - wall_t/2]:
            mat = Matrix.Translation(Vector((0, py, pz))) @ Matrix.Diagonal((w - post_s * 1.5, plank_h * 0.92, wall_t, 1.0))
            bmesh.ops.create_cube(bm, size=1.0, matrix=mat)
        # East / West walls
        for px in [-w/2 + wall_t/2, w/2 - wall_t/2]:
            mat = Matrix.Translation(Vector((px, py, 0))) @ Matrix.Diagonal((wall_t, plank_h * 0.92, d - post_s * 1.5, 1.0))
            bmesh.ops.create_cube(bm, size=1.0, matrix=mat)
            
    bm.to_mesh(mesh)
    bm.free()
    
    export_fbx(obj, "Hay_Barn_Crate.fbx")

def create_pitchfork():
    reset_scene()
    mesh = bpy.data.meshes.new("Hay_Pitchfork_Tool")
    obj = bpy.data.objects.new("Hay_Pitchfork_Tool", mesh)
    bpy.context.scene.collection.objects.link(obj)
    
    bm = bmesh.new()
    
    # 1. Wooden Handle (Shaft)
    handle_len = 1.30
    handle_r = 0.018
    # Remember Blender 5.0 cone for cylinder
    mat_handle = Matrix.Translation(Vector((0, -handle_len * 0.5, 0)))
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=10, radius1=handle_r, radius2=handle_r, depth=handle_len, matrix=mat_handle)
    
    # 2. Metal Collar/Socket
    collar_len = 0.08
    collar_r = 0.022
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=10, radius1=collar_r, radius2=collar_r*1.2, depth=collar_len, matrix=Matrix.Translation(Vector((0, 0.04, 0))))
    
    # 3. Cross Bar / Crown
    crown_w = 0.28
    crown_r = 0.012
    mat_crown = Matrix.Translation(Vector((0, 0.08, 0))) @ Matrix.Rotation(math.radians(90), 4, 'Z')
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=8, radius1=crown_r, radius2=crown_r, depth=crown_w, matrix=mat_crown)
    
    # 4. Three Curved Iron Tines (Prongs)
    tine_len = 0.32
    tine_r = 0.007
    for offset_x in [-0.11, 0.0, 0.11]:
        # Tine with curve forward along Z
        mat_tine = Matrix.Translation(Vector((offset_x, 0.08 + tine_len * 0.5, 0.04))) @ Matrix.Rotation(math.radians(-12), 4, 'X')
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=8, radius1=tine_r, radius2=0.001, depth=tine_len, matrix=mat_tine)
        
    bm.to_mesh(mesh)
    bm.free()
    
    export_fbx(obj, "Hay_Pitchfork_Tool.fbx")

if __name__ == "__main__":
    print("Generating Haystack Simulator 3D Assets...")
    create_straw()
    create_needle()
    create_barn_crate()
    create_pitchfork()
    print("All 3D assets generated successfully!")
