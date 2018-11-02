// ts2fable 0.6.1
module ReactMonacoEditor
open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Helpers.React
open Fable.Import
open Fable.Import.JS
open Fable.Import.React
open Monaco

// module MonacoEditor = Monaco_editor

// type [<AllowNullLiteral>] IExports =
//     abstract MonacoEditor: MonacoEditorStatic
//     abstract MonacoDiffEditor: MonacoDiffEditorStatic

type ChangeHandler = string -> MonacoEditor.Editor.IModelContentChangedEvent -> unit

type EditorDidMount = MonacoEditor.Editor.IStandaloneCodeEditor -> obj -> unit

type EditorWillMount = obj -> unit

type MonacoEditorProp =
    /// Width of editor. Defaults to 100%.
    | Width of U2<string, float>
    /// Height of editor. Defaults to 500.
    | Height of U2<string, float>
    /// The initial value of the auto created model in the editor.
    | DefaultValue of string
    /// The initial language of the auto created model in the editor. Defaults to 'javascript'.
    | Language of string
    /// Theme to be used for rendering.
    /// The current out-of-the-box available themes are: 'vs' (default), 'vs-dark', 'hc-black'.
    /// You can create custom themes via `monaco.editor.defineTheme`.
    | Theme of string
    /// Optional, allow to config loader url and relative path of module, refer to require.config.
    | RequireConfig of obj
    /// Optional, allow to pass a different context then the global window onto which the monaco instance will be loaded. Useful if you want to load the editor in an iframe.
    | Context of obj
    /// Value of the auto created model in the editor.
    /// If you specify value property, the component behaves in controlled mode. Otherwise, it behaves in uncontrolled mode.
    | Value of string
    /// Refer to Monaco interface {monaco.editor.IEditorConstructionOptions}.
    | Options of MonacoEditor.Editor.IEditorConstructionOptions
    /// An event emitted when the editor has been mounted (similar to componentDidMount of React).
    | EditorDidMount of EditorDidMount
    /// An event emitted before the editor mounted (similar to componentWillMount of React).
    | EditorWillMount of EditorWillMount
    /// An event emitted when the content of the current model has changed.
    | OnChange of ChangeHandler

let inline monacoEditor (props : MonacoEditorProp list) (elems : ReactElement list) : ReactElement =
    ofImport "default" "react-monaco-editor" (keyValueList CaseRules.LowerFirst props) elems

// type [<AllowNullLiteral>] MonacoEditorBaseProps =
//     /// Width of editor. Defaults to 100%.
//     abstract width: U2<string, float> option with get, set
//     /// Height of editor. Defaults to 500.
//     abstract height: U2<string, float> option with get, set
//     /// The initial value of the auto created model in the editor.
//     abstract defaultValue: string option with get, set
//     /// The initial language of the auto created model in the editor. Defaults to 'javascript'.
//     abstract language: string option with get, set
//     /// Theme to be used for rendering.
//     /// The current out-of-the-box available themes are: 'vs' (default), 'vs-dark', 'hc-black'.
//     /// You can create custom themes via `monaco.editor.defineTheme`.
//     abstract theme: string option with get, set
//     /// Optional, allow to config loader url and relative path of module, refer to require.config.
//     abstract requireConfig: obj option with get, set
//     /// Optional, allow to pass a different context then the global window onto which the monaco instance will be loaded. Useful if you want to load the editor in an iframe.
//     abstract context: obj option with get, set

// type [<AllowNullLiteral>] MonacoEditorProps =
//     inherit MonacoEditorBaseProps
//     /// Value of the auto created model in the editor.
//     /// If you specify value property, the component behaves in controlled mode. Otherwise, it behaves in uncontrolled mode.
//     abstract value: string option with get, set
//     /// Refer to Monaco interface {monaco.editor.IEditorConstructionOptions}.
//     abstract options: MonacoEditor.Editor.IEditorConstructionOptions option with get, set
//     /// An event emitted when the editor has been mounted (similar to componentDidMount of React).
//     abstract editorDidMount: EditorDidMount option with get, set
//     /// An event emitted before the editor mounted (similar to componentWillMount of React).
//     abstract editorWillMount: EditorWillMount option with get, set
//     /// An event emitted when the content of the current model has changed.
//     abstract onChange: ChangeHandler option with get, set

// type [<AllowNullLiteral>] MonacoEditor =
//     inherit React.Component<MonacoEditorProps>

// type [<AllowNullLiteral>] MonacoEditorStatic =
//     [<Emit "new $0($1...)">] abstract Create: unit -> MonacoEditor

// type [<AllowNullLiteral>] DiffEditorDidMount =
//     [<Emit "$0($1...)">] abstract Invoke: editor: MonacoEditor.Editor.IStandaloneDiffEditor * monaco: obj -> unit

// type [<AllowNullLiteral>] DiffEditorWillMount =
//     [<Emit "$0($1...)">] abstract Invoke: monaco: obj -> unit

// type [<AllowNullLiteral>] DiffChangeHandler =
//     [<Emit "$0($1...)">] abstract Invoke: value: string -> unit

// type [<AllowNullLiteral>] MonacoDiffEditorProps =
//     inherit MonacoEditorBaseProps
//     /// The original value to compare against.
//     abstract original: string option with get, set
//     /// Value of the auto created model in the editor.
//     /// If you specify value property, the component behaves in controlled mode. Otherwise, it behaves in uncontrolled mode.
//     abstract value: string option with get, set
//     /// Refer to Monaco interface {monaco.editor.IDiffEditorConstructionOptions}.
//     abstract options: MonacoEditor.Editor.IDiffEditorConstructionOptions option with get, set
//     /// An event emitted when the editor has been mounted (similar to componentDidMount of React).
//     abstract editorDidMount: DiffEditorDidMount option with get, set
//     /// An event emitted before the editor mounted (similar to componentWillMount of React).
//     abstract editorWillMount: DiffEditorWillMount option with get, set
//     /// An event emitted when the content of the current model has changed.
//     abstract onChange: DiffChangeHandler option with get, set

// type [<AllowNullLiteral>] MonacoDiffEditor =
//     inherit React.Component<MonacoDiffEditorProps>

// type [<AllowNullLiteral>] MonacoDiffEditorStatic =
//     [<Emit "new $0($1...)">] abstract Create: unit -> MonacoDiffEditor