<#
.SYNOPSIS
    VOK-H4-E3 §1 — replay pesan dari DLQ (queue "<Queue>_error") balik ke queue asal.

.DESCRIPTION
    Dipakai runbook operator & (nanti) panel Health SA (H6) saat DLQ terisi krn bug yang SUDAH
    diperbaiki (deploy baru) - memindahkan pesan yang sebelumnya gagal permanen supaya diproses
    ulang, TANPA perlu tulis ulang manual dari log.

    Pakai RabbitMQ Management HTTP API (bukan client AMQP native - PowerShell tak py satu bawaan,
    dan management API SUDAH aktif krn image "rabbitmq:3-management-alpine" dipakai project ini,
    lihat docker-compose.yml) - dua panggilan per pesan: GET (konsumsi, ack_requeue_false) dari
    "<Queue>_error", lalu POST publish body+properties APA ADANYA ke exchange default dgn routing
    key = <Queue>. Mekanisme SAMA PERSIS dgn DlqReplayTests.cs (Vokasia.Tests/Async) - hanya beda
    bahasa (AMQP client C# di test, HTTP API di sini) krn keterbatasan PowerShell, BUKAN logika yg
    berbeda.

.PARAMETER Queue
    Nama queue ASAL (BUKAN nama DLQ-nya) - mis. "journal-approved-consumer". Skrip otomatis
    menambah suffix "_error" utk membaca DLQ-nya (lihat MessagingDefaults.DeadLetterQueueSuffix,
    Vokasia.Infrastructure/Messaging/MessagingDefaults.cs - konvensi bawaan MassTransit).

.PARAMETER Count
    Jumlah pesan yang di-replay (default 10). Kalau DLQ berisi kurang dari itu, berhenti lebih awal.

.PARAMETER ManagementUrl
    Base URL RabbitMQ Management API. Default http://localhost:15672 (port yang di-publish
    docker-compose.yml utk service rabbitmq).

.PARAMETER Username / -Password
    Default "vokasia"/"vokasia_dev" (SAMA dgn RABBITMQ_USER/RABBITMQ_PASS di .env) - override kalau beda.

.EXAMPLE
    .\Replay-Dlq.ps1 -Queue "journal-approved-consumer" -Count 5
#>
param(
    [Parameter(Mandatory = $true)][string]$Queue,
    [int]$Count = 10,
    [string]$ManagementUrl = "http://localhost:15672",
    [string]$Username = "vokasia",
    [string]$Password = "vokasia_dev",
    [string]$VHost = "/"
)

$ErrorActionPreference = "Stop"

$dlqName = "$Queue`_error"
$vhostEncoded = [uri]::EscapeDataString($VHost)
$pair = "$($Username):$($Password)"
$basicAuth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($pair))
$headers = @{ Authorization = "Basic $basicAuth"; "Content-Type" = "application/json" }

Write-Host "Replay-Dlq: memindahkan hingga $Count pesan dari '$dlqName' -> '$Queue' ($ManagementUrl)..."

$moved = 0
for ($i = 0; $i -lt $Count; $i++) {
    $getBody = @{ count = 1; ackmode = "ack_requeue_false"; encoding = "auto"; truncate = 50000 } | ConvertTo-Json
    $getUrl = "$ManagementUrl/api/queues/$vhostEncoded/$dlqName/get"

    try {
        $messages = Invoke-RestMethod -Uri $getUrl -Method Post -Headers $headers -Body $getBody
    }
    catch {
        Write-Warning "Gagal GET dari '$dlqName' (mungkin sudah kosong atau belum pernah ada pesan): $($_.Exception.Message)"
        break
    }

    if (-not $messages -or $messages.Count -eq 0) {
        Write-Host "DLQ '$dlqName' kosong - berhenti setelah $moved pesan dipindah."
        break
    }

    $msg = $messages[0]

    $publishBody = @{
        properties       = $msg.properties
        routing_key      = $Queue
        payload          = $msg.payload
        payload_encoding = $msg.payload_encoding
    } | ConvertTo-Json -Depth 10

    $publishUrl = "$ManagementUrl/api/exchanges/$vhostEncoded/amq.default/publish"
    $result = Invoke-RestMethod -Uri $publishUrl -Method Post -Headers $headers -Body $publishBody

    if ($result.routed) {
        $moved++
        Write-Host "  [$moved/$Count] pesan dipindah balik ke '$Queue'."
    }
    else {
        Write-Warning "  Publish ke '$Queue' TIDAK ter-routed (queue tak ada consumer/binding?) - pesan ini HILANG dari DLQ tanpa sampai tujuan, cek manual."
    }
}

Write-Host "Replay-Dlq selesai: $moved pesan dipindah dari '$dlqName' ke '$Queue'."
