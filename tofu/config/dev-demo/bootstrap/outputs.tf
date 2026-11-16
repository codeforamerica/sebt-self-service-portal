output "nameservers" {
  description = "NS records for the demo hosted zone. Send these to the CfA DNS maintainer for delegation."
  value       = aws_route53_zone.main.name_servers
}
