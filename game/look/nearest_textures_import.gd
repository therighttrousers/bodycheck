@tool
extends EditorScenePostImport

## Gives the materials embedded in an imported model nearest filtering, since smoothed textures took hardware that the
## games of the look didn't have. glTF's own filter settings don't survive the import. A material saved in a file of
## its own sets its filter there.


func _post_import(scene: Node) -> Object:
	_set_filters(scene)
	return scene


func _set_filters(node: Node) -> void:
	if node is MeshInstance3D and node.mesh != null:
		for i in node.mesh.get_surface_count():
			var material: Material = node.mesh.surface_get_material(i)
			# A material saved in a file of its own is only borrowed here. A change wouldn't reach the file, so it would
			# show in the editor, which keeps the file loaded, and not in the game.
			if material is BaseMaterial3D and material.resource_path.is_empty():
				material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	for child in node.get_children():
		_set_filters(child)
