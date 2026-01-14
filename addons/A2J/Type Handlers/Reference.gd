class_name A2JReferenceTypeHandler extends A2JTypeHandler


func _init() -> void:
	error_strings = [
		'"references" in ruleset should be structured as follows: Dictionary[String,Variant].',
		'Reference name should be a String.',
	]


## Should not be used.
func to_json(_value, _ruleset:Dictionary) -> void:
	pass


func from_json(json:Dictionary, ruleset:Dictionary) -> Variant:
	var named_references = ruleset.get('references',{})
	if named_references is not Dictionary:
		report_error(0)
		return null

	var name = json.get('name','')
	if name is not String:
		report_error(1)
		return null
	name = name as String

	if name.begins_with('.i'):
		var ids_to_objects = A2J._process_data.get('ids_to_objects', {})
		if ids_to_objects is Dictionary:
			var id:String = name.split('.i')[1]
			return ids_to_objects.get(id, '_A2J_unresolved_reference')

	return named_references.get(name, null)
