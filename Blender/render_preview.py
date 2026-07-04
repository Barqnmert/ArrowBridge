# Renders the current scene (run after generate_arrowbridge_environment.py in the
# same headless Blender session) to a still PNG using the scene's active camera.
import bpy

scene = bpy.context.scene

try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"

scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = r"C:\Users\baran\AppData\Local\Temp\claude\C--Users-baran-Arrow-Bridge\0cee21f3-4a1a-4937-b742-f0515181ca3a\scratchpad\arrowbridge_preview.png"

bpy.ops.render.render(write_still=True)
print("Rendered to", scene.render.filepath)
