# AI Chatbot (AWS Bedrock)

A multi-modal chatbot web app: pick any AWS Bedrock model (or **Automatic** routing),
chat with streaming responses, generate and analyze images, upload documents/images,
use voice input/output, and keep per-user chat history organized into sessions. The UI
adapts its layout to each model's capabilities (chat / image studio / video studio).

## Features

- **Model picker** listing every available Bedrock model, plus an **Automatic** option.
- **Capability filter** to narrow models by what they do (image generation, vision,
  streaming, document input, etc.).
- **Intent-aware Automatic routing** — image/video-generation requests and image-analysis
  (with an attached image) are steered to the right model; everything else goes to a text model.
- **Image generation** (Stability text-to-image models) and **vision** (image understanding
  on models that accept image input).
- **Streaming** text responses (SSE), with graceful in-chat error messages.
- **Voice**: speak your prompt and have replies read back aloud.
- **Files**: upload docx/xlsx (parsed to text) and images; stored in S3.
- **Auth**: Cognito login (no self-signup — users are created by an admin). Disabled
  automatically for local development.

## Stack

| Layer      | Tech |
|------------|------|
| Frontend   | React + TypeScript + Vite + Tailwind + DaisyUI |
| Backend    | .NET 10 Web API, EF Core (PostgreSQL) |
| AI         | AWS Bedrock (Converse / streaming + InvokeModel for images), "Automatic" router (Amazon Nova Micro) |
| Speech     | Amazon Polly (TTS) + browser Web Speech (STT), Amazon Transcribe optional |
| Storage    | S3 (files/media) + PostgreSQL/RDS (sessions, messages, metadata) |
| Auth       | Amazon Cognito |
| Hosting    | ECS Fargate (Spot) + ALB (Dockerized) |
| CI/CD      | AWS CodePipeline + CodeBuild |

## Regions

- **S3, Polly, Transcribe** run in `AWS_REGION` (default **us-east-1**).
- **Bedrock** runs in `BEDROCK_REGION` (default **us-west-2**) because the on-demand
  text-to-image models (`stability.stable-image-core/ultra`, `sd3-5-large`) are available
  there. The two are configured independently, so your S3 bucket can stay in us-east-1.

## Repo layout

```
backend/     .NET 10 Web API
frontend/    React SPA
infra/       CloudFormation (foundation.yaml, service.yaml)
pipeline/    CodePipeline template + buildspecs
docker-compose.yml
```

## Authentication

- **Local dev:** auth is disabled (`DisableAuth=true`), so you're auto-signed in as a dev user.
- **Deployed:** the backend validates Cognito JWTs and the SPA shows a **Sign in** page.
  There is **no self-signup** — an admin creates users. The public `/api/config` endpoint
  tells the SPA the Cognito region/client id and whether auth is on.

### Create users manually

```powershell
$pool = "<your-user-pool-id>"   # from the chatbot-foundation stack outputs

aws cognito-idp admin-create-user --user-pool-id $pool --username "alice@example.com" --message-action SUPPRESS --region us-east-1
aws cognito-idp admin-set-user-password --user-pool-id $pool --username "alice@example.com" --password "Str0ngPass!" --permanent --region us-east-1
```

Password policy: ≥8 chars with upper, lower and a number. If you skip the second command,
the user is prompted to set a new password on first sign-in (the login page handles that).

## Local development

1. Copy `.env.example` to `.env` and fill in your dev IAM credentials. `AWS_REGION`
   defaults to us-east-1 (S3/Polly) and `BEDROCK_REGION` to us-west-2 (image models).
2. No manual Bedrock model-access step is needed (see below).
3. Run:

   ```powershell
   docker compose up --build
   ```

   - Frontend: http://localhost:3000
   - Backend API + Swagger: http://localhost:8080/swagger

Auth is disabled locally (`DisableAuth` in `appsettings.Development.json`).

### Run without Docker

```powershell
# Terminal 1 - backend
cd backend
dotnet run

# Terminal 2 - frontend
cd frontend
npm install
npm run dev   # http://localhost:5173 (proxies /api to :8080)
```

---

## AWS setup guide (what you need to create)

Most infrastructure is created for you by CloudFormation. Only a few things must exist first.

### 1. Create a dev IAM user (for local development)

1. AWS Console → **IAM** → **Users** → **Create user** → name `chatbot-dev`.
2. Attach policies: `AmazonBedrockFullAccess`, `AmazonS3FullAccess`,
   `AmazonPollyFullAccess`, `AmazonTranscribeFullAccess`.
3. **Security credentials** → **Create access key** → *Application running outside AWS*.
4. Put the key id/secret into your local `.env` (⚠️ never commit or paste secrets in chat).

### 2. Enable Bedrock model access

AWS **retired the manual "Model access" page** — serverless models auto-enable on first
invocation. You generally don't need to do anything, with two exceptions:

- **Anthropic (Claude):** first-time use may require submitting a short use-case form
  (do it once from the Bedrock **Model catalog** / playground).
- **AWS Marketplace models:** a user with Marketplace permissions must invoke them once.

Access is otherwise governed by IAM. Image generation uses **us-west-2** (see Regions above).

### 3. Create a GitHub connection (for the pipeline)

1. Console → **Developer Tools** → **Settings** → **Connections** → **Create connection** → GitHub.
2. Authorize it and copy the **Connection ARN** — you'll pass it to the pipeline stack.

### 4. Deploy the infrastructure

```powershell
# Foundation: VPC, S3, RDS, Cognito, ECR
aws cloudformation deploy `
  --stack-name chatbot-foundation `
  --template-file infra/foundation.yaml `
  --capabilities CAPABILITY_IAM `
  --parameter-overrides DBPassword=<STRONG_PASSWORD>

# Build & push images once (or let the pipeline do it), then:
aws cloudformation deploy `
  --stack-name chatbot-service `
  --template-file infra/service.yaml `
  --capabilities CAPABILITY_IAM `
  --parameter-overrides `
    BackendImage=<ecr-uri>/chatbot-backend:latest `
    FrontendImage=<ecr-uri>/chatbot-frontend:latest `
    DBPassword=<STRONG_PASSWORD>
```

The **AppUrl** output of `chatbot-service` is your public URL.

### 5. Deploy the CI/CD pipeline (optional, automates step 4)

```powershell
aws cloudformation deploy `
  --stack-name chatbot-pipeline `
  --template-file pipeline/pipeline.yaml `
  --capabilities CAPABILITY_IAM `
  --parameter-overrides `
    GitHubConnectionArn=<connection-arn> `
    GitHubRepo=<owner/repo> `
    DBPassword=<STRONG_PASSWORD>
```

---

## Values to send me

To finish wiring things up, share these **non-secret** values (never the access key/secret):

- AWS region for storage (default `us-east-1`) and Bedrock region (default `us-west-2`)
- After deploying `chatbot-foundation`: the stack **Outputs** (bucket name, user pool id,
  user pool client id, ECR repo URIs) — visible in the CloudFormation console.
- GitHub connection ARN + `owner/repo` (if using the pipeline)

## Notes & next steps

- **Cost-optimized infra (demo):** `foundation.yaml` has **no NAT Gateway** (ECS tasks run in
  public subnets with public IPs, firewalled by security groups) and a free **S3 gateway
  endpoint**; `service.yaml` runs ECS on **Fargate Spot**. Baseline ~$40–45/mo.
- Speech-to-text currently uses the browser Web Speech API; Amazon Transcribe can be
  wired in for higher accuracy via presigned upload + a Transcribe job.
- **Video generation** (Nova Reel) is not wired yet — it uses the asynchronous
  `StartAsyncInvoke` + S3 polling flow.
- Generated images are referenced by **presigned S3 URLs** (valid 7 days) so they render
  in the browser without an auth header.
- The deployed app is served over **HTTP** (no TLS). Add an ACM certificate + HTTPS listener
  (and a domain) for a production-grade, secure URL.
- EF Core migrations run automatically on backend startup.
