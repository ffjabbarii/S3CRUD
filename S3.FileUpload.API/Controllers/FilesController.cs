﻿using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using S3.FileUpload.API.Models;

namespace S3.FileUpload.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly IAmazonS3 _s3Client;

        public FilesController(IAmazonS3 s3Client)
        {
            _s3Client = s3Client;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFileAsync(IFormFile file, string bucketName, string? prefix)
        {
            var bucketExists = await _s3Client.DoesS3BucketExistAsync(bucketName);

            if (!bucketExists)
            {
                return BadRequest(new { Message = $"Bucket {bucketName} does not exists." });
            }

            var request = new PutObjectRequest()
            {
                BucketName = bucketName,
                Key = string.IsNullOrEmpty(prefix) ? file.FileName : $"{prefix?.TrimEnd('/')}/{file.FileName}",
                InputStream = file.OpenReadStream()
            };
            request.Metadata.Add("Content-Type", file.ContentType);

            await _s3Client.PutObjectAsync(request);

            return Ok(new { Message = $"File {prefix}/{file.FileName} uploaded to S3 successfully!" });
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllFilesAsync(string bucketName, string? prefix)
        {
            var bucketExists = await _s3Client.DoesS3BucketExistAsync(bucketName);

            if (!bucketExists)
            {
                return BadRequest(new { Message = $"Bucket {bucketName} does not exists." });
            }

            var request = new ListObjectsV2Request()
            {
                BucketName = bucketName,
                Prefix = prefix
            };

            var result = await _s3Client.ListObjectsV2Async(request);

            var s3Objects = result.S3Objects.Select(s =>
            {
                var urlRequest = new GetPreSignedUrlRequest()
                {
                    BucketName = bucketName,
                    Key = s.Key,
                    Expires = DateTime.UtcNow.AddMinutes(1)
                };

                return new S3ObjectDto()
                {
                    Name = s.Key.ToString(),
                    PresignedUrl = _s3Client.GetPreSignedURL(urlRequest),
                };
            });

            return Ok(new { Objects = s3Objects });
        }

        [HttpGet("get-by-key")]
        public async Task<IActionResult> GetFileByKeyAsync(string bucketName, string key)
        {
            var bucketExists = await _s3Client.DoesS3BucketExistAsync(bucketName);

            if (!bucketExists)
            {
                return BadRequest(new { Message = $"Bucket {bucketName} does not exists." });
            }

            try
            {
                await _s3Client.GetObjectMetadataAsync(bucketName, key);

                var s3Object = await _s3Client.GetObjectAsync(bucketName, key);

                return File(s3Object.ResponseStream, s3Object.Headers.ContentType);
            }
            catch (AmazonS3Exception ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return BadRequest(new { Message = $"Object {key} does not exists." });

                throw;
            }
        }

        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteFileAsync(string bucketName, string key)
        {
            var bucketExists = await _s3Client.DoesS3BucketExistAsync(bucketName);

            if (!bucketExists)
            {
                return BadRequest(new { Message = $"Bucket {bucketName} does not exists." });
            }

            try
            {
                await _s3Client.GetObjectMetadataAsync(bucketName, key);

                await _s3Client.DeleteObjectAsync(bucketName, key);

                return NoContent();
            }
            catch (AmazonS3Exception ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return BadRequest(new { Message = $"Object {key} does not exists." });

                throw;
            }
        }
    }
}
