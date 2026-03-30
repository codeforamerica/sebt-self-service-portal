import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RichText } from './RichText'

describe('RichText', () => {
  it('renders plain text unchanged', () => {
    render(<RichText>Hello world</RichText>)
    expect(screen.getByText('Hello world')).toBeInTheDocument()
  })

  it('renders bold markdown as strong element', () => {
    const { container } = render(<RichText>This is **bold** text</RichText>)
    const strong = container.querySelector('strong')
    expect(strong).toBeInTheDocument()
    expect(strong?.textContent).toBe('bold')
  })

  it('renders newline-separated paragraphs as separate p tags', () => {
    const { container } = render(<RichText>{'Paragraph one\n\nParagraph two'}</RichText>)
    const paragraphs = container.querySelectorAll('p')
    expect(paragraphs).toHaveLength(2)
    expect(paragraphs[0].textContent).toBe('Paragraph one')
    expect(paragraphs[1].textContent).toBe('Paragraph two')
  })

  it('renders inline mode without wrapping p tags', () => {
    const { container } = render(<RichText inline>This is **bold** inline</RichText>)
    expect(container.querySelector('p')).not.toBeInTheDocument()
    expect(container.querySelector('strong')?.textContent).toBe('bold')
  })
})
