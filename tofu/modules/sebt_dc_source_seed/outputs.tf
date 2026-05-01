output "cluster_name" {
  description = "ECS cluster name on which to run the seed task. Passthrough."
  value       = var.cluster_name
}

output "log_group_name" {
  description = "CloudWatch log group name for the seed task. Useful for tailing logs after the task completes."
  value       = aws_cloudwatch_log_group.task.name
}

output "security_group_id" {
  description = "Security group ID for the run-task --network-configuration. Passthrough."
  value       = var.security_group_id
}

output "subnet_ids" {
  description = "Private subnet IDs for the run-task --network-configuration. Passthrough."
  value       = var.subnet_ids
}

output "task_definition_family" {
  description = "Task definition family name (no revision). Pass to `aws ecs run-task --task-definition` to use the latest revision."
  value       = aws_ecs_task_definition.this.family
}
