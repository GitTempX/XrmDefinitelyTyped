﻿module internal DG.XrmDefinitelyTyped.CreateFormDts

open TsStringUtil
open IntermediateRepresentation

/// Translate internal attribute type to corresponding TypeScript interface.
let getAttributeInterface = function
  | AttributeType.OptionSet ty  -> TsType.SpecificGeneric ("Xrm.OptionSetAttribute", [ ty ])
  | AttributeType.Default ty    -> TsType.SpecificGeneric ("Xrm.Attribute", [ ty ])
  | x                           -> TsType.Custom (sprintf "Xrm.%AAttribute" x)
 
/// Gets the corresponding enum of the option set if possible
let getOptionSetType = function
  | Some (AttributeType.OptionSet ty) -> ty
  | _ -> TsType.Number

/// Translate internal control type to corresponding TypeScript interface.
let getControlInterface cType aType =
  match aType, cType with
  | None, ControlType.Default       -> TsType.Custom "Xrm.BaseControl"
  | Some (AttributeType.Default TsType.String), ControlType.Default
                                    -> TsType.Custom "Xrm.StringControl"
  | Some at, ControlType.Default    -> TsType.SpecificGeneric ("Xrm.Control", [ getAttributeInterface at ]) 
  | aType, ControlType.OptionSet    -> TsType.SpecificGeneric ("Xrm.OptionSetControl", [ getOptionSetType aType ])
  | _, x                            -> TsType.Custom (sprintf "Xrm.%AControl" x)

/// Default collection functions which also use the "get" function name.
let defaultCollectionFuncs defaultType = 
  [ Function.Create("get", 
      [ Variable.Create("name", TsType.String) ], TsType.Undefined)

    Function.Create("get", [], TsType.Array (TsType.Custom defaultType))
    Function.Create("get", 
      [Variable.Create("index", TsType.Number)], TsType.Custom defaultType)
    Function.Create("get", 
      [Variable.Create("chooser", 
        TsType.Function(
          [ Variable.Create("item", TsType.Custom defaultType)
            Variable.Create("index", TsType.Number) ], 
          TsType.Boolean))], 
        TsType.Array (TsType.Custom defaultType))]


/// Generate Xrm.Page.data.entity.attributes.get(<string>) functions.
let getAttributeCollection (attributes: XrmFormAttribute list) =
  let getFuncs = 
    attributes
    |> List.map (fun (name,ty) ->
      let paramType = getConstantType name
      let returnType = getAttributeInterface ty
      Function.Create("get", [Variable.Create("name", paramType)], returnType))

  let defaultFuncs = defaultCollectionFuncs "Xrm.Attribute<any>"
  Interface.Create("Attributes", extends = ["Xrm.AttributeCollectionBase"],
    funcs = getFuncs @ defaultFuncs)


/// Generate Xrm.Page.ui.controls.get(<string>) functions.
let getControlCollection (controls: XrmFormControl list) =
  let getFuncs = 
    controls
    |> List.map (fun (name, aType, cType) ->
      let paramType = getConstantType name
      let returnType = getControlInterface cType aType          
      Function.Create("get", [Variable.Create("name", paramType)], returnType))

  let defaultFuncs = defaultCollectionFuncs "Xrm.BaseControl"
  Interface.Create("Controls", extends = ["Xrm.ControlCollectionBase"],
    funcs = getFuncs @ defaultFuncs)


/// Generate Xrm.Page.ui.tabs.get(<string>) functions.
let getTabCollection (tabs: XrmFormTab list) =
  let getFuncs =
    tabs
    |> List.map (fun (iname, name, sections) ->
      let paramType = getConstantType name
      let returnType = sprintf "Xrm.PageTab<Tabs.%s>" iname |> TsType.Custom
      Function.Create("get", [Variable.Create("name", paramType)], returnType))

  let defaultFuncs = 
    defaultCollectionFuncs 
      "Xrm.PageTab<Xrm.Collection<Xrm.PageSection>>"

  Interface.Create("Tabs", extends = ["Xrm.TabCollectionBase"],
    funcs = getFuncs @ defaultFuncs)


/// Generate Xrm.Page.ui.tabs.get(<someTab>).sections.get(<string>) functions.
let getSectionCollections (tabs: XrmFormTab list) =
  let getFuncs sections = 
    sections
    |> List.map (fun name -> 
      let paramType = getConstantType name
      Function.Create("get", [ Variable.Create("name", paramType) ], 
        TsType.Custom "Xrm.PageSection"))

  let defaultFuncs = defaultCollectionFuncs "Xrm.PageSection"
  tabs |> List.map (fun (iname, name, sections) ->
    Interface.Create(iname, extends = ["Xrm.SectionCollectionBase"],
      funcs = getFuncs sections @ defaultFuncs))



/// Generate Xrm.Page.getAttribute(<string>) functions.
let getAttributeFuncs (attributes: XrmFormAttribute list) =
  let attrFuncs = 
    attributes
    |> List.map (fun (name, ty) ->
      let paramType = getConstantType name
      let returnType = getAttributeInterface ty
      Function.Create("getAttribute", 
        [ Variable.Create("attributeName", paramType) ], returnType))

  let defaultFunc =
    Function.Create("getAttribute", 
      [ Variable.Create("attributeName", TsType.String) ], 
      TsType.Undefined )
  attrFuncs @ [ defaultFunc ]


  
/// Generate Xrm.Page.getControl(<string>) functions.
let getControlFuncs (controls: XrmFormControl list) =
  let ctrlFuncs = 
    controls
    |> List.map (fun (name, aType, cType) ->
      let paramType = getConstantType name
      let returnType = getControlInterface cType aType
      Function.Create("getControl", 
        [ Variable.Create("controlName", paramType) ], returnType))

  let defaultFunc =
    Function.Create("getControl", 
      [ Variable.Create("controlName", TsType.String) ], 
      TsType.Undefined)
  ctrlFuncs @ [ defaultFunc ]



/// Generate internal namespace for keeping track all the collections.
let getFormNamespace (form: XrmForm) =
  Namespace.Create(form.name,
    interfaces = 
      [ getAttributeCollection form.attributes 
        getControlCollection form.controls 
        getTabCollection form.tabs ],
    namespaces = 
      [ Namespace.Create("Tabs", interfaces = getSectionCollections form.tabs) ])


/// Generate the interface for the Xrm.Page of the form.
let getFormInterface (form: XrmForm) =
  let superClass = 
    sprintf "Xrm.PageBase<%s.Attributes,%s.Tabs,%s.Controls>"
      form.name form.name form.name

  Interface.Create(form.name, extends = [superClass], 
    funcs = 
      getAttributeFuncs form.attributes @ 
      getControlFuncs form.controls)


/// Generate the namespace containing all the form interface and internal 
/// namespaces for collections.
let getFormDts (form: XrmForm) = 
  let nsName = 
    sprintf "Form.%s%s" 
      (form.entityName |> Utility.sanitizeString)
      (match form.formType with
      | Some ty -> sprintf ".%s" ty
      | None   -> "")

  Namespace.Create(
    nsName,
    declare = true,
    namespaces = [ getFormNamespace form ],
    interfaces = [ getFormInterface form ]) 
  |> nsToString

