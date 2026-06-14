using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Groups.Api
{
    [ApiController]
    [Route("api/groups/{groupId:long}/boards")]
    [Authorize]
    public class GroupBoardController(GroupBoardService service) : ControllerBase
    {
        private readonly GroupBoardService _service = service;

        [HttpGet]
        [SwaggerOperation(Summary = "List boards visible to the caller")]
        public async Task<ActionResult<List<GroupBoardGetResponseDto>?>> List([FromRoute] long groupId)
        {
            var response = await _service.ListAsync(groupId);
            if (response is null) return NoContent();
            return Ok(response);
        }

        [HttpPost]
        [SwaggerOperation(Summary = "Create a board (admin/owner)")]
        public async Task<ActionResult<GroupBoardGetResponseDto>> Create(
            [FromRoute] long groupId,
            [FromBody] GroupBoardCreateRequestDto request)
        {
            var response = await _service.CreateAsync(groupId, request);
            return Created($"/api/groups/{groupId}/boards/{response.Id}", response);
        }

        [HttpPatch("{boardId:long}")]
        [SwaggerOperation(Summary = "Update a board (admin/owner)")]
        public async Task<ActionResult<GroupBoardGetResponseDto>> Update(
            [FromRoute] long groupId,
            [FromRoute] long boardId,
            [FromBody] GroupBoardPatchRequestDto request)
        {
            var response = await _service.UpdateAsync(groupId, boardId, request);
            return Ok(response);
        }

        [HttpDelete("{boardId:long}")]
        [SwaggerOperation(Summary = "Delete a board (admin/owner)")]
        public async Task<IActionResult> Delete([FromRoute] long groupId, [FromRoute] long boardId)
        {
            await _service.DeleteAsync(groupId, boardId);
            return NoContent();
        }
    }
}
