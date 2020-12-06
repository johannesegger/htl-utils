namespace GenerateITInformationSheet.Configuration

open System.IO

type Config = {
    DocumentTemplatePath: string
    ContentTemplatePath: string
    HeaderTemplatePath: string
    FooterTemplatePath: string
    FileNameTemplate: string
}
module Config =
    let fromEnvironment () =
        {
            DocumentTemplatePath = Environment.getEnvVarOrFail "MGMT_IT_INFORMATION_SHEET_DOCUMENT_TEMPLATE_PATH"
            ContentTemplatePath = Environment.getEnvVarOrFail "MGMT_IT_INFORMATION_SHEET_CONTENT_TEMPLATE_PATH"
            HeaderTemplatePath = Environment.getEnvVarOrFail "MGMT_IT_INFORMATION_SHEET_HEADER_TEMPLATE_PATH"
            FooterTemplatePath = Environment.getEnvVarOrFail "MGMT_IT_INFORMATION_SHEET_FOOTER_TEMPLATE_PATH"
            FileNameTemplate = Environment.getEnvVarOrFail "MGMT_IT_INFORMATION_SHEET_FILE_NAME_TEMPLATE"
        }
