import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  Badge,
  Button,
  Card,
  CardBody,
  CardHeader,
  ConfirmDialog,
  EmptyState,
  Modal,
} from '@/components/ui'
import { QueryBoundary } from '@/components/common/QueryBoundary'
import { GroupRole } from '@/types/api'
import { boardVisibilityLabels, boardWriteabilityLabels } from '../labels'
import { BoardForm, type BoardFormValues } from '../components/BoardForm'
import { useBoards, useCreateBoard, useDeleteBoard, useUpdateBoard } from '../boardsHooks'
import { useMyGroupRole } from '../membersHooks'
import type { GroupBoard } from '../boardsApi'

export function GroupBoardsPage() {
  const { id } = useParams<{ id: string }>()
  const groupId = Number(id)
  const valid = Number.isFinite(groupId)
  const boards = useBoards(valid ? groupId : null)
  const { role } = useMyGroupRole(valid ? groupId : null)
  const canManage = role === GroupRole.Owner || role === GroupRole.Admin

  const createBoard = useCreateBoard(groupId)
  const [createOpen, setCreateOpen] = useState(false)

  const onCreate = (values: BoardFormValues) => {
    createBoard.mutate(
      {
        name: values.name,
        description: values.description,
        visibility: values.visibility,
        writeability: values.writeability,
      },
      { onSuccess: () => setCreateOpen(false) },
    )
  }

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <Link to={`/groups/${groupId}`} className="text-sm text-blue-600 hover:underline">
        Back to group
      </Link>
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">Boards</h1>
        {canManage && <Button onClick={() => setCreateOpen(true)}>New board</Button>}
      </div>

      <QueryBoundary
        isLoading={boards.isLoading}
        isError={boards.isError}
        onRetry={() => boards.refetch()}
      >
        {boards.data && boards.data.length > 0 ? (
          <ul className="flex flex-col gap-2">
            {boards.data.map((board) => (
              <li key={board.id}>
                <BoardRow groupId={groupId} board={board} canManage={canManage} />
              </li>
            ))}
          </ul>
        ) : (
          <EmptyState title="No boards yet" />
        )}
      </QueryBoundary>

      <Modal isOpen={createOpen} title="New board" onClose={() => setCreateOpen(false)}>
        <BoardForm
          submitLabel="Create"
          isPending={createBoard.isPending}
          error={createBoard.error}
          onSubmit={onCreate}
          onCancel={() => setCreateOpen(false)}
        />
      </Modal>
    </div>
  )
}

function BoardRow({
  groupId,
  board,
  canManage,
}: {
  groupId: number
  board: GroupBoard
  canManage: boolean
}) {
  const updateBoard = useUpdateBoard(groupId)
  const deleteBoard = useDeleteBoard(groupId)
  const [editOpen, setEditOpen] = useState(false)
  const [confirmOpen, setConfirmOpen] = useState(false)

  const onEdit = (values: BoardFormValues) => {
    updateBoard.mutate(
      {
        boardId: board.id,
        body: {
          name: values.name,
          description: values.description,
          visibility: values.visibility,
          writeability: values.writeability,
        },
      },
      { onSuccess: () => setEditOpen(false) },
    )
  }

  return (
    <Card>
      <CardHeader className="flex items-center gap-2">
        <Link
          to={`/groups/${groupId}/boards/${board.id}`}
          className="text-base font-semibold text-gray-900 hover:underline"
        >
          {board.name}
        </Link>
        <div className="flex flex-wrap gap-1">
          <Badge>{boardVisibilityLabels[board.visibility]}</Badge>
          <Badge color="gray">{boardWriteabilityLabels[board.writeability]}</Badge>
        </div>
        {canManage && (
          <div className="ml-auto flex gap-2">
            <Button size="sm" variant="secondary" onClick={() => setEditOpen(true)}>
              Edit
            </Button>
            <Button size="sm" variant="danger" onClick={() => setConfirmOpen(true)}>
              Delete
            </Button>
          </div>
        )}
      </CardHeader>
      {board.description && (
        <CardBody>
          <p className="text-sm text-gray-600">{board.description}</p>
        </CardBody>
      )}

      <Modal isOpen={editOpen} title="Edit board" onClose={() => setEditOpen(false)}>
        <BoardForm
          initial={{
            name: board.name,
            description: board.description ?? '',
            visibility: board.visibility,
            writeability: board.writeability,
          }}
          submitLabel="Save"
          isPending={updateBoard.isPending}
          error={updateBoard.error}
          onSubmit={onEdit}
          onCancel={() => setEditOpen(false)}
        />
      </Modal>

      <ConfirmDialog
        isOpen={confirmOpen}
        title="Delete board"
        message="This permanently deletes the board and its posts."
        confirmLabel="Delete"
        destructive
        isLoading={deleteBoard.isPending}
        onConfirm={() =>
          deleteBoard.mutate(board.id, { onSuccess: () => setConfirmOpen(false) })
        }
        onCancel={() => setConfirmOpen(false)}
      />
    </Card>
  )
}
