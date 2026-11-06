# **Infrastructure Summary**

## AWS Services

The following AWS services are used to run the Summer EBT portal and enrollment checker in Code for America's development environments:

| Component | AWS Service | Technical details | In plain English |
| --- | --- | --- | --- |
| Shared | **Virtual Private Cloud (VPC) / Elastic Compute Cloud (EC2) networking** | One VPC with public and private subnets, an internet gateway, a NAT gateway, and security groups | Private network within AWS that the applications run inside, to keep sensitive pieces (eg, the database) off the public internet |
| Shared | **Route 53** | Hosted zones, records, and certificate validation | The internet's address book (DNS). It points website addresses to the right place. |
| Shared | **Certificate Manager (ACM)** | TLS certificates for the ALBs and both CloudFront distributions, validated by DNS | Provides security certificates that let the sites load over `https` (the padlock in the browser) |
| Shared | **Key Management Service (KMS)** | Customer-managed keys for logs, RDS, ElastiCache, Secrets Manager, and the enrollment checker bucket | Manages encryption keys that protect stored data, so only approved systems can read it |
| Shared | **S3** (logging) | Central log bucket | File storage for system log files |
| Shared | **CloudWatch Logs** | Logs for ECS, RDS (error and agent), Lambda, and WAF. Log forwarding is done by the Datadog Forwarder Lambda, which is installed separately through Datadog's CloudFormation stack, not by OpenTofu. | Collects activity and error messages from each part of the system for auditing and so the team can find and fix problems. Also used to power monitors and alerting in connected 3rd party observability tools (eg. Datadog).
| Shared | **Identity and Access Management (IAM)** | ECS task and execution roles, Lambda roles, the GitHub Actions deploy user, the SES SMTP user, and roles used by Doppler to sync secrets. | Controls which people and systems are allowed to do/access what in AWS. |
| Shared | **AWS Secrets Manager** | JWT signing key, identifier hasher key, Smarty address validation credentials, SES SMTP credentials, DB and Redis credentials, PII encryption keys, case management system API credentials, OIDC credentials | Secure storage for required passwords and keys needed to access different services and dependencies, so they are never written into the code. |
| Shared | **Lambda** | Two rotation jobs: one for the RDS app-user password (alternating between two users) and one for the SES SMTP credentials. Both run every 30 days, then restart the API so it picks up the new credentials. | Runs small automated jobs. In this project, it changes the database and email-sending passwords on a schedule. |
| Portal | **Elastic Container Registry (ECR)** | Image repositories for `api` and `web`, with scan-on-push. | Stores the packaged versions of the code that is ready to deploy. |
| Portal | **Elastic Container Service (ECS) on Fargate (serverless)** | **API** (.NET, port 8080) and **Web** (Next.js, port 3000) Sidecars: ADOT collector on both, AppConfig agent on the API. ECS Exec is on. | Runs the portal’s front-end and the back-end service behind it, without the team having to manage servers |
| Portal | **Elastic Load Balancing (Application Load Balancer)** | Internal ALB for the API (`api.<domain>`). ALB for Web (`origin.<domain>`) that accepts traffic only from the CloudFront prefix list | Spreads incoming traffic requests across multiple running copies of the application, and sends those requests only to healthy copies. |
| Portal | **CloudFront** | Sits in front of the Web ALB. | The “front door” for anyone who visits the site. It makes pages load faster and adds a layer of protection. |
| Portal | **Web Application Firewall (WAF) v2** | Web ACL on the CloudFront distribution, with a rate-limit rule. Currently passive for both dev environments using `passive_waf = true`. | A firewall for web traffic, set up to block suspicious or abusive requests. In dev it's currently in a watch-only mode that logs them instead of blocking. |
| Portal | **RDS (SQL Server DB)** | SQL Server Express, 7-day backups, KMS encryption, private subnets. | The application database, which stores key application data such as user accounts and preferences, ID verification progress, card replacement requests, and enrollment check results (with children's details de-identified). |
| Portal | **ElastiCache (Valkey 7.2)** | 2 nodes (one primary, one replica), Multi-AZ, automatic failover, TLS, auth token. Holds the distributed cache, locks, and the OIDC pre-auth session store. | A fast, short-term memory that all running copies of the application share. For example, it stores session info to keep a user's login working, no matter which copy handles their request. |
| Portal | **EC2 + Systems Manager** | SSM bastion instance. Developers reach the database through SSM port forwarding, without SSH keys. | A secure entry point that lets approved developers access the product database. |
| Portal | **AWS AppConfig** | One application and environment, with a feature flags profile and an app settings profile | Lets the team turn product features on or off, and change settings, without releasing a new version of the app. |
| Portal | **Simple Email Service (SES)** | Domain identity for the portal domain, with DKIM, SPF (`bounce.<domain>`), and DMARC records in Route 53. Sends through SMTP using an IAM user whose credentials are stored in Secrets Manager. | Sends emails from the portal, such as one-time login tokens. |
| Enrollment checker | **S3** | Private, versioned site bucket with KMS encryption, access logging, and a lifecycle policy. The site is static, so it needs no compute. | Stores the enrollment checker's web pages (as a static website). |
| Enrollment checker | **CloudFront** | Serves the bucket through Origin Access Control, `PriceClass_100`. A CloudFront Function rewrites URIs for the Next.js static export. | Delivers the enrollment checker's pages to visitors quickly. |
| Enrollment checker | **ACM + Route 53** | Certificate and alias record for `dev.<state>.sebt-enrollment.codeforamerica.app`. | Gives the enrollment checker its web address and its `https` padlock. |
| Enrollment checker | **KMS** | Key for the site bucket | Encrypts the enrollment checker's stored files |
| State backend | **S3 + DynamoDB + KMS** | OpenTofu state bucket, lock table, and encryption key | Keeps a record of the infrastructure that has been built, so changes are applied safely and one at a time. |

## External secrets management tool

We use [Doppler](https://www.doppler.com/) for secrets management, which get added to AWS via. These secrets include keys for services for identity verification, state case management system APIs, address validation, monitoring, and analytics.

## How these pieces work together

### Portal

- When someone first visits the site, **AWS Route 53** uses the url to direct them to the underlying location of the site. **ACM** provides the secure connection (`https`).
- The user technically reaches **CloudFront** first, which contains the **WAF** firewall to screen out abusive traffic.
- CloudFront passes the user’s request to a **load balancer**, which picks a healthy running copy of the application to present to them.
- The front-end of the application (`web`) runs on **ECS/Fargate** inside a private network (**VPC**). It asks the back-end service (`api`) for data.
- The API is reachable only from inside the private network, through its own internal load balancer.
- The API gets its data from several places:
  - The application database (**RDS / MS SQL Server**) for key portal data.
  - Shared short-term memory (**ElastiCache / Valkey, a Redis-compatible cache**) for things like a user's logged-in session.
  - State systems of record - i.e., the **case management API** for retrieving summer EBT household data or enrollment status
  - Service (OIDC-based) for user login/authorization
  - If configured, an identity proofing 
- The API sends emails such as one-time login tokens through **Amazon SES**
- Any passwords and keys that the application relies on are stored in **AWS Secrets Manager**. Feature flags and settings are set up and can be changed from **AppConfig**.

### Enrollment checker

- This application is a set of static web pages stored in **Amazon S3** and delivered by its own **CloudFront**, with its own address and certificate.
- No additional servers or databases are involved. **It uses the same API back end as the portal, but reaches it through the portal's public website, not directly:** the visitor's browser sends enrollment checks to the portal's address, where they pass through CloudFront and the WAF like any other portal visitor, and the portal's front end (web) passes them on to the API.

### Behind the scenes

- **KMS** encrypts stored data throughout.
- **IAM** controls what each component or user is allowed to do or access
- **CloudWatch** collects application server logs, and some are forwarded to an external observability tool for ease of navigation, and creating metrics, alerts, and dashboards
- **Lambda** jobs change the database and email-sending passwords every 30 days, per security best practices
- Developers can access the database only through a locked-down entry point (the **SSM bastion**).

## How the app gets built and updated

- Some one-time setup lives in a separate bootstrap configuration (`tofu/config/<env>/bootstrap`) that an admin applies by hand: the OpenTofu state bucket and lock table, the ECR image repositories, the GitHub Actions deploy user, and the enrollment checker's DNS zone. The automated deploy doesn't change these.
- Before merge: every change goes through a pull request in **GitHub**. The repo ruleset requires the `Build & Test` check (from the State CI workflow, `.github/workflows/state-ci.yaml`) to pass, and the PR needs a label saying which state(s) it applies to.
- After merge to the main branch, two workflows start at the same time:
  - The deploy workflow (`.github/workflows/deploy-ecr.yaml`), for both `dev-dc` and `dev-co`:
    - Packages the back end (API) and front end (web) code into container images and uploads them to AWS ECR
    - Runs **OpenTofu plan and apply** based on the tofu infrastructure-as-code files:
      - It points **ECS** at the new images in **ECR**. ECS starts new copies of the app, and retires the old ones once the new ones are healthy
      - It applies any infrastructure changes in the same commit
- If the enrollment checker code is changed, `packages/**` has changes, or `pnpm-lock.yaml` has changes:
  - An additional workflow (`.github/workflows/deploy-enrollment-checker.yaml`) is used, which compiles and builds the static site and writes a settings file (config.js) with the API URL, portal URL, and analytics keys
  - It uploads the files to the **AWS S3 bucket** and clears the CloudFront cache, so visitors will see the new version right away
