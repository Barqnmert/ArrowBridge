# ============================================================================
# ArrowBridge - procedural 3D environment generator for Blender
# ----------------------------------------------------------------------------
# HOW TO RUN
#   Blender -> Scripting tab -> Open this file -> Run Script (Alt+P).
#   Or headless: blender --background --python generate_arrowbridge_environment.py
#   Run this in a NEW/EMPTY .blend file - clear_scene() deletes every object
#   currently in the file.
#
# WHAT THIS BUILDS
#   Water + two land masses (Kara A / Kara B), a segmented 3D bridge deck with
#   trusses spanning the gap between them, one sample "maze arrow" piece built
#   flat/thin (2D look) the way the puzzle pieces read in-game, and a capsule
#   placeholder character standing on Kara A. Plus camera, sun and a
#   background color, so you get a framed shot out of the box.
#
# WHY THE NUMBERS LOOK LIKE THIS
#   Every size/position constant below is copied from the live gameplay code
#   (BridgeBuilder.cs, ArrowGridManager.cs, ArrowController.cs, GamePalette.cs)
#   so this scene lines up with the values the game already runs on. If you
#   retune spacing/size in those C# files, mirror the change here too.
#
# EXPORTING BACK TO UNITY
#   Blender is Z-up, Unity is Y-up. This script treats:
#     Blender X       -> Unity X  (bridge span, left/right)
#     Blender Z (up)  -> Unity Y  (up; bridge/segment height)
#     Blender Y       -> Unity Z  (depth - the 2D game never used this axis;
#                                  it's free for you to art-direct)
#   Blender's default FBX export axes (Forward: -Z Forward, Up: Y Up) already
#   produce this mapping on import, so no manual rotation is needed. Keep
#   "Apply Scalings: FBX All" on export and the Unity Model Importer's Scale
#   Factor at 1 so 1 Blender unit stays 1 Unity unit.
# ============================================================================

import bpy
import bmesh
import math

# ----- Bridge (BridgeBuilder.cs defaults) -----
BRIDGE_TOTAL_SEGMENTS = 30
BRIDGE_START_X = -6.0
BRIDGE_END_X = 6.0
BRIDGE_Y = 0.0
BRIDGE_SEGMENT_HEIGHT = 0.5
BRIDGE_TRUSS_EVERY_N = 3
BRIDGE_TRUSS_HEIGHT = 0.4
BRIDGE_TRUSS_WIDTH_FACTOR = 0.9
BRIDGE_DECK_DEPTH = 1.4  # new depth axis (Unity Z) - not in the 2D game, tune freely

# ----- Arrow puzzle grid (ArrowGridManager.cs defaults) -----
ARROW_MIN_X, ARROW_MAX_X = -8, 8
ARROW_MIN_Y, ARROW_MAX_Y = 3, 8
ARROW_CELL_SIZE = 1.0
ARROW_PLANE_DEPTH_Y = -2.2  # where the puzzle board sits along the new depth axis

# ----- Arrow piece visual tuning (ArrowController.cs defaults) -----
ARROW_LINE_THICKNESS = 0.18
ARROW_HEAD_LENGTH = 0.34
ARROW_HEAD_WIDTH_FACTOR = 2.6
ARROW_FLAT_DEPTH = 0.05  # thin on purpose: arrows stay visually 2D, not sculpted 3D

# A sample bent 3-cell "maze" path, just to show the puzzle-piece look.
# Format matches ArrowController's pathCells + exitDirection. Replace freely.
SAMPLE_ARROW_PATH_CELLS = [(-2, 5), (-1, 5), (-1, 6), (0, 6)]
SAMPLE_ARROW_EXIT_DIRECTION = "Right"

# ----- Character -----
CHARACTER_RADIUS = 0.3
CHARACTER_HEIGHT = 0.9

# ----- Land / water -----
LAND_EXTRA_X = 4.0
LAND_HEIGHT = 2.0
LAND_DEPTH = BRIDGE_DECK_DEPTH * 2.5
WATER_MARGIN = 20.0

# True = unlit Emission materials, matching the game's current flat-sprite look.
# False = lit Principled BSDF, if you want the scene to read as a normal 3D render.
FLAT_SHADING = True

# Hex colors copied 1:1 from GamePalette.cs
COLORS = {
    "Water": "#3B8BD4",
    "Land": "#8B8378",
    "ArrowBody": "#1B1B26",
    "BridgePrimary": "#1B998B",
    "BridgeAccentDark": "#0F6E56",
    "Background": "#EAF4FB",
    "Character": "#D85A30",
}


# ----------------------------------------------------------------------------
# Helpers
# ----------------------------------------------------------------------------

def hex_to_linear(hex_color):
    hex_color = hex_color.lstrip("#")
    r, g, b = (int(hex_color[i:i + 2], 16) / 255.0 for i in (0, 2, 4))

    def to_linear(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

    return (to_linear(r), to_linear(g), to_linear(b), 1.0)


def make_material(name, hex_color):
    mat = bpy.data.materials.new(name=f"M_{name}")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    color = hex_to_linear(hex_color)

    if FLAT_SHADING:
        emission = nodes.new("ShaderNodeEmission")
        emission.inputs["Color"].default_value = color
        emission.inputs["Strength"].default_value = 1.0
        links.new(emission.outputs["Emission"], output.inputs["Surface"])
    else:
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = 0.9
        links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])

    mat.diffuse_color = color
    return mat


def assign_material(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block_collection in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for block in list(block_collection):
            if block.users == 0:
                block_collection.remove(block)


def add_box(name, size_xyz, location, material=None, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size_xyz
    if material:
        assign_material(obj, material)
    if parent:
        parent_keep_transform(obj, parent)
    return obj


def add_triangular_prism(name, direction, length, base_width, depth, location, material=None, parent=None):
    """A thin triangular wedge extruded along Blender Y (depth).
    direction is one of 'up'/'down'/'left'/'right' in the Blender XZ plane
    (matches ArrowDirection.Up/Down/Left/Right one-to-one), apex pointing that way.
    """
    half_base = base_width / 2.0
    half_depth = depth / 2.0

    if direction == "up":
        base = [(-half_base, 0.0), (half_base, 0.0)]
        apex = (0.0, length)
    elif direction == "down":
        base = [(-half_base, 0.0), (half_base, 0.0)]
        apex = (0.0, -length)
    elif direction == "right":
        base = [(0.0, -half_base), (0.0, half_base)]
        apex = (length, 0.0)
    else:  # left
        base = [(0.0, -half_base), (0.0, half_base)]
        apex = (-length, 0.0)

    mesh = bpy.data.meshes.new(f"{name}_mesh")
    bm = bmesh.new()
    front = [bm.verts.new((x, -half_depth, z)) for x, z in (base[0], base[1], apex)]
    back = [bm.verts.new((x, half_depth, z)) for x, z in (base[0], base[1], apex)]
    bm.faces.new(front)
    bm.faces.new((back[2], back[1], back[0]))
    for i in range(3):
        j = (i + 1) % 3
        bm.faces.new((front[i], front[j], back[j], back[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    if material:
        assign_material(obj, material)
    if parent:
        parent_keep_transform(obj, parent)
    return obj


def add_cylinder(name, radius, depth_along_y, location, material=None, parent=None, segments=16):
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=depth_along_y, location=location,
                                         rotation=(math.radians(90), 0, 0), vertices=segments)
    obj = bpy.context.active_object
    obj.name = name
    if material:
        assign_material(obj, material)
    if parent:
        parent_keep_transform(obj, parent)
    return obj


def add_empty(name, location=(0, 0, 0)):
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_size = 0.5
    empty.location = location
    bpy.context.collection.objects.link(empty)
    return empty


def parent_keep_transform(obj, parent):
    """Parents obj to parent without moving it - plain `obj.parent = parent` would
    re-apply the parent's transform on top of obj's existing world position."""
    obj.parent = parent
    obj.matrix_parent_inverse = parent.matrix_world.inverted()


# ----------------------------------------------------------------------------
# Scene pieces
# ----------------------------------------------------------------------------

def create_environment(materials):
    root = add_empty("Environment")

    land_top_z = BRIDGE_Y + BRIDGE_SEGMENT_HEIGHT / 2.0
    water_z = land_top_z - LAND_HEIGHT * 0.4

    land_a_width = LAND_EXTRA_X
    land_a_center_x = BRIDGE_START_X - land_a_width / 2.0
    add_box("Land_A", (land_a_width, LAND_DEPTH, LAND_HEIGHT),
            (land_a_center_x, 0.0, land_top_z - LAND_HEIGHT / 2.0),
            materials["Land"], root)

    land_b_width = LAND_EXTRA_X
    land_b_center_x = BRIDGE_END_X + land_b_width / 2.0
    add_box("Land_B", (land_b_width, LAND_DEPTH, LAND_HEIGHT),
            (land_b_center_x, 0.0, land_top_z - LAND_HEIGHT / 2.0),
            materials["Land"], root)

    add_box("Water", (WATER_MARGIN, WATER_MARGIN, 0.1), (0.0, 0.0, water_z),
            materials["Water"], root)

    return root, land_top_z


def create_bridge(materials):
    root = add_empty("Bridge", location=(0, 0, BRIDGE_Y))
    segment_width = (BRIDGE_END_X - BRIDGE_START_X) / BRIDGE_TOTAL_SEGMENTS

    for index in range(BRIDGE_TOTAL_SEGMENTS):
        center_x = BRIDGE_START_X + segment_width * (index + 0.5)
        add_box(f"BridgeSegment_{index:02d}", (segment_width * 1.05, BRIDGE_DECK_DEPTH, BRIDGE_SEGMENT_HEIGHT),
                (center_x, 0.0, BRIDGE_Y), materials["BridgePrimary"], root)

        if (index + 1) % BRIDGE_TRUSS_EVERY_N == 0:
            add_triangular_prism(f"Truss_{index:02d}", "up",
                                  BRIDGE_TRUSS_HEIGHT, segment_width * BRIDGE_TRUSS_WIDTH_FACTOR,
                                  BRIDGE_DECK_DEPTH,
                                  (center_x, 0.0, BRIDGE_Y + BRIDGE_SEGMENT_HEIGHT / 2.0),
                                  materials["BridgeAccentDark"], root)

    return root, segment_width


def cell_to_world(cell):
    col, row = cell
    return (col * ARROW_CELL_SIZE, ARROW_PLANE_DEPTH_Y, row * ARROW_CELL_SIZE)


DIRECTION_STEP = {
    "up": (0, 1),
    "down": (0, -1),
    "left": (-1, 0),
    "right": (1, 0),
}


def create_sample_arrow(materials):
    root = add_empty("Arrow_Sample")
    cells = SAMPLE_ARROW_PATH_CELLS
    joint_radius = ARROW_LINE_THICKNESS / 2.0

    for i, cell in enumerate(cells):
        x, y, z = cell_to_world(cell)
        add_cylinder(f"ArrowNode_{i}", joint_radius, ARROW_FLAT_DEPTH, (x, y, z),
                     materials["ArrowBody"], root, segments=12)

        if i > 0:
            prev_x, prev_y, prev_z = cell_to_world(cells[i - 1])
            mid = ((x + prev_x) / 2.0, y, (z + prev_z) / 2.0)
            horizontal = abs(x - prev_x) > abs(z - prev_z)
            dims = (ARROW_CELL_SIZE, ARROW_FLAT_DEPTH, ARROW_LINE_THICKNESS) if horizontal \
                else (ARROW_LINE_THICKNESS, ARROW_FLAT_DEPTH, ARROW_CELL_SIZE)
            add_box(f"ArrowConnector_{i}", dims, mid, materials["ArrowBody"], root)

    exit_direction = SAMPLE_ARROW_EXIT_DIRECTION.lower()
    step = DIRECTION_STEP[exit_direction]
    last_x, last_y, last_z = cell_to_world(cells[-1])
    head_base = (last_x + step[0] * ARROW_CELL_SIZE * 0.5, last_y, last_z + step[1] * ARROW_CELL_SIZE * 0.5)
    add_triangular_prism("Arrow_Head", exit_direction, ARROW_HEAD_LENGTH,
                          ARROW_LINE_THICKNESS * ARROW_HEAD_WIDTH_FACTOR, ARROW_FLAT_DEPTH,
                          head_base, materials["ArrowBody"], root)

    return root


def create_character(materials, land_top_z):
    root = add_empty("PlayerCharacter", location=(BRIDGE_START_X - 1.0, 0.0, land_top_z))
    cylinder_height = max(0.01, CHARACTER_HEIGHT - 2 * CHARACTER_RADIUS)
    center_z = land_top_z + CHARACTER_HEIGHT / 2.0

    bpy.ops.mesh.primitive_cylinder_add(radius=CHARACTER_RADIUS, depth=cylinder_height,
                                         location=(root.location.x, 0.0, center_z))
    body = bpy.context.active_object
    body.name = "Character_Body"

    bpy.ops.mesh.primitive_uv_sphere_add(radius=CHARACTER_RADIUS,
                                          location=(root.location.x, 0.0, center_z + cylinder_height / 2.0))
    top_cap = bpy.context.active_object
    top_cap.name = "Character_TopCap"

    bpy.ops.mesh.primitive_uv_sphere_add(radius=CHARACTER_RADIUS,
                                          location=(root.location.x, 0.0, center_z - cylinder_height / 2.0))
    bottom_cap = bpy.context.active_object
    bottom_cap.name = "Character_BottomCap"

    bpy.ops.object.select_all(action="DESELECT")
    for part in (body, top_cap, bottom_cap):
        part.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()

    character = bpy.context.active_object
    character.name = "PlayerCharacter_Mesh"
    assign_material(character, materials["Character"])
    parent_keep_transform(character, root)
    return root


def setup_camera_and_light(land_top_z):
    focus = add_empty("SceneFocus", location=(0.0, 0.0, land_top_z))

    camera_data = bpy.data.cameras.new("MainCamera")
    camera = bpy.data.objects.new("MainCamera", camera_data)
    camera.location = (0.0, -16.0, land_top_z + 8.0)
    bpy.context.collection.objects.link(camera)
    track = camera.constraints.new(type="TRACK_TO")
    track.target = focus
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    bpy.context.scene.camera = camera

    sun_data = bpy.data.lights.new("Sun", type="SUN")
    sun_data.energy = 3.0
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(55), math.radians(15), math.radians(35))
    bpy.context.collection.objects.link(sun)

    world = bpy.context.scene.world
    if world is None:
        world = bpy.data.worlds.new("World")
        bpy.context.scene.world = world
    world.use_nodes = True
    bg_node = world.node_tree.nodes.get("Background")
    if bg_node:
        bg_node.inputs[0].default_value = hex_to_linear(COLORS["Background"])


def apply_all_transforms():
    bpy.ops.object.select_all(action="DESELECT")
    mesh_objects = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    for obj in mesh_objects:
        obj.select_set(True)
    if mesh_objects:
        bpy.context.view_layer.objects.active = mesh_objects[0]
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)


# ----------------------------------------------------------------------------
# Main
# ----------------------------------------------------------------------------

def main():
    clear_scene()
    materials = {key: make_material(key, hex_color) for key, hex_color in COLORS.items()}

    _, land_top_z = create_environment(materials)
    create_bridge(materials)
    create_sample_arrow(materials)
    create_character(materials, land_top_z)
    setup_camera_and_light(land_top_z)
    apply_all_transforms()

    print(f"ArrowBridge environment generated: {len(bpy.data.objects)} objects.")


if __name__ == "__main__":
    main()
