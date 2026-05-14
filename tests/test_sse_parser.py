"""Tests for popup.parse_chunk — pure SSE chunk parser."""
import popup


def test_parse_chunk_extracts_content():
    line = 'data: {"choices":[{"delta":{"content":"Hello"}}]}'
    assert popup.parse_chunk(line) == "Hello"


def test_parse_chunk_handles_no_data_prefix():
    line = '{"choices":[{"delta":{"content":"X"}}]}'
    assert popup.parse_chunk(line) == "X"


def test_parse_chunk_returns_none_for_done_sentinel():
    assert popup.parse_chunk("data: [DONE]") is None


def test_parse_chunk_returns_none_for_empty():
    assert popup.parse_chunk("") is None


def test_parse_chunk_returns_none_for_malformed_json():
    assert popup.parse_chunk("data: not json") is None


def test_parse_chunk_returns_none_when_delta_lacks_content():
    line = 'data: {"choices":[{"delta":{"role":"assistant"}}]}'
    assert popup.parse_chunk(line) is None


def test_parse_chunk_returns_none_when_choices_empty():
    line = 'data: {"choices":[]}'
    assert popup.parse_chunk(line) is None


def test_parse_chunk_returns_none_when_choices_missing():
    assert popup.parse_chunk('data: {}') is None


def test_parse_chunk_handles_unicode():
    line = 'data: {"choices":[{"delta":{"content":"こんにちは"}}]}'
    assert popup.parse_chunk(line) == "こんにちは"
