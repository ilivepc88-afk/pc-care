# SignPath Foundation 申请与启用清单

本项目已按 SignPath Foundation 的开源申请要求准备 Apache-2.0 许可证、隐私声明、代码签名策略和 GitHub Actions 信任构建骨架。签名服务未获批前，不能设置 `SIGNPATH_ENABLED=true`。

## 申请前检查

- [ ] GitHub 仓库保持公开；所有源代码、构建脚本和发布工作流均在仓库中。
- [ ] 已阅读根目录 `LICENSE`、`NOTICE`、`CODE_SIGNING_POLICY.md`、`SECURITY.md` 与 `docs/PRIVACY.md`。
- [ ] 不含公司内部配置、账号、令牌、证书、私有源代码或不应公开的业务信息。
- [ ] 已有公开的 GitHub Release；当前为 `v0.6.0`。
- [ ] 所有维护者已开启 GitHub 多因素认证。
- [ ] README、Release 页面与应用内确认提示已明确描述系统修改范围。

## 申请信息

在 <https://signpath.org/apply> 提交以下信息：

- 项目：`PcCare`
- 仓库：`https://github.com/ilivepc88-afk/pc-care`
- 许可证：Apache License 2.0
- 发行页：`https://github.com/ilivepc88-afk/pc-care/releases`
- 功能概述：离线 Windows 检查与保守优化；不收集/传输数据，不含恶意软件、漏洞利用或安全绕过功能。
- 签名策略页：仓库根目录 `CODE_SIGNING_POLICY.md`

SignPath Foundation 最终决定是否接纳项目；免费订阅的证书发布者为 SignPath Foundation，而非项目维护者个人。

## 获批后的 SignPath 配置

1. 按 SignPath 引导创建或加入 Organization，并将 GitHub.com Trusted Build System 关联到 PcCare Project。
2. 安装 SignPath GitHub App，仅授权 `ilivepc88-afk/pc-care`。
3. 创建一个仅用于正式发布的 signing policy；每次请求仍需要人工批准。
4. 创建 artifact configuration，输入是 GitHub Actions 默认 ZIP 产物，根目录下必须包含：

   ```text
   offline/PcCare.exe
   lite/PcCare.exe
   ```

   配置必须保留上述相对路径、对两个 EXE 应用 Authenticode 签名，并限制 `ProductName=PcCare` 与统一版本元数据。
5. 由 SignPath 创建具有 submitter 权限的 API token。该 token 只存入 GitHub Secret，绝不放入代码或 Release。

## GitHub 配置

进入 `Settings → Secrets and variables → Actions`，新增：

| 类型 | 名称 | 值 |
|---|---|---|
| Secret | `SIGNPATH_API_TOKEN` | SignPath API token |
| Variable | `SIGNPATH_ORGANIZATION_ID` | SignPath Organization ID |
| Variable | `SIGNPATH_PROJECT_SLUG` | PcCare Project slug |
| Variable | `SIGNPATH_SIGNING_POLICY_SLUG` | 正式发行 signing policy slug |
| Variable | `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG` | ZIP artifact configuration slug |
| Variable | `SIGNPATH_ENABLED` | 先留空；全部验证完成后设为 `true` |

`SIGNPATH_ENABLED` 不是密钥。保持为空或 `false` 时，标签构建只生成未签名 Actions 构件，不会创建 GitHub Release。

## 发布操作

1. 更新 `src/PcCare.App/PcCare.App.csproj` 中的 `Version`/`FileVersion`、`README.md` 当前版本和 `CHANGELOG.md`。
2. 合并到 `main` 并等待普通构建通过。
3. 创建并推送匹配标签，例如 `v0.6.1`。
4. 标签工作流构建、测试并上传未签名构件给 SignPath。
5. 在 SignPath 页面核对仓库、提交、标签、版本、两个 EXE 路径后人工批准。
6. 工作流拿到签名结果后重新计算 SHA256，上传签名构件并创建 GitHub Release。
7. 下载 Release 文件，在 Windows 文件属性的“数字签名”中验证签名；再校验 SHA256。

## 失败处理

- 不要重用或公开 API token；通过 GitHub Secrets 轮换泄露 token。
- 若 SignPath 返回的目录布局不同，先调整本仓库工作流的 `Create signed checksums and release files` 路径，再发布新标签。
- 若签名请求超时，不发布未签名替代资产；检查 SignPath 审批状态或重新创建一个新的、版本号递增的标签。
- `.github/CODEOWNERS` 已标出工作流、签名策略与安全策略文件；在 GitHub 分支保护中启用 Code Owners 审查后才会实际强制执行。
